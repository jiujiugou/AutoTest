using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using AutoTest.Application;
using AutoTest.Application.Dto;
using AutoTest.Core.AI;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;

namespace AutoTest.Infrastructure.Log;

public sealed class LogService : ILogService
{
    private readonly ElasticsearchClient _elasticClient;
    private const string IndexPattern = "autotest-logs-*";

    public LogService(ElasticsearchClient elasticClient)
    {
        _elasticClient = elasticClient;
    }

    public Task ClearAsync()
    {
        // For Elasticsearch, "clear" might mean deleting the index or just a no-op 
        // since logs are centrally managed. We'll return completed.
        return Task.CompletedTask;
    }

    public async Task<LogPageDto> QueryAsync(LogQueryDto query)
    {
        var take = query.Take <= 0 ? 200 : query.Take;
        if (take > 500) take = 500;

        var queries = new List<Query>();

        if (!string.IsNullOrWhiteSpace(query.Level))
        {
            var lvl = query.Level.Trim().ToUpperInvariant();
            var serilogLvl = lvl switch
            {
                "INFO" => "Information",
                "WARN" => "Warning",
                "ERROR" => "Error",
                "FATAL" => "Fatal",
                "DEBUG" => "Debug",
                _ => lvl
            };
            queries.Add(new TermQuery { Field = "level.keyword", Value = serilogLvl });
        }

        if (!string.IsNullOrWhiteSpace(query.Module))
        {
            var mod = query.Module.Trim();
            queries.Add(new MatchPhraseQuery { Field = "message", Query = mod });
        }

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = query.Keyword.Trim();
            queries.Add(new MatchQuery { Field = "message", Query = kw });
        }

        if (query.FromUtc.HasValue || query.ToUtc.HasValue)
        {
            var r = new DateRangeQuery { Field = "@timestamp" };
            if (query.FromUtc.HasValue) r.Gte = query.FromUtc.Value;
            if (query.ToUtc.HasValue) r.Lte = query.ToUtc.Value;
            queries.Add(r);
        }

        var searchRequest = new SearchRequest<ElasticLogDocument>(Indices.Parse(IndexPattern))
        {
            Size = take,
            Sort = new List<SortOptions>
            {
                new SortOptions { Field = new FieldSort { Field = "@timestamp", Order = SortOrder.Desc } }
            }
        };

        if (queries.Count > 0)
        {
            searchRequest.Query = new BoolQuery { Must = queries };
        }

        // Handle cursor (search_after)
        if (!string.IsNullOrWhiteSpace(query.Before))
        {
            try
            {
                var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(query.Before));
                var parts = decoded.Split('|');
                if (parts.Length >= 2 && long.TryParse(parts[1], out var ticks))
                {
                    searchRequest.SearchAfter = new[] { Elastic.Clients.Elasticsearch.FieldValue.Long(ticks) };
                }
            }
            catch { /* Ignore invalid cursor */ }
        }

        SearchResponse<ElasticLogDocument> response;
        try
        {
            response = await _elasticClient.SearchAsync<ElasticLogDocument>(searchRequest);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Elasticsearch 查询失败：无法连接或请求异常。", ex);
        }

        if (!response.IsValidResponse)
        {
            throw new InvalidOperationException(
                $"Elasticsearch 查询失败：\n{response.DebugInformation}"
            );
        }
        var items = new List<LogItemDto>();
        foreach (var hit in response.Hits)
        {
            var doc = hit.Source;
            if (doc == null) continue;

            var timestamp = doc.Timestamp;
            var tsLocal = timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            
            var level = doc.Level switch
            {
                "Information" => "INFO",
                "Warning" => "WARN",
                "Error" => "ERROR",
                "Fatal" => "FATAL",
                "Debug" => "DEBUG",
                "Verbose" => "TRACE",
                _ => doc.Level?.ToUpperInvariant() ?? "INFO"
            };

            var message = doc.Message ?? "";
            if (!string.IsNullOrEmpty(doc.Exception))
            {
                message += $"\n{doc.Exception}";
            }

            var cursorRaw = $"{hit.Id}|{timestamp.ToUniversalTime().Ticks}";
            var cursor = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(cursorRaw));

            items.Add(new LogItemDto(cursor, tsLocal, level, "Webapi", message));
        }

        var hasMore = items.Count == take;
        var next = hasMore ? items.Last().Cursor : null;

        return new LogPageDto(items, next, hasMore);
    }

    public async Task<List<ElasticLogDocument>> GetAiErrorContextAsync(
    string traceId,
    DateTime? errorTime = null,
    int windowSeconds = 30,
    int take = 120)
    {
        var must = new List<Query>
    {
        new TermQuery
        {
            Field = "traceId.keyword",
            Value = traceId
        }
    };

        var should = new List<Query>
    {
        new TermQuery { Field = "level.keyword", Value = "Error" },
        new TermQuery { Field = "level.keyword", Value = "Fatal" }
    };

        var filter = new List<Query>
    {
        new BoolQuery
        {
            Should = should,
            MinimumShouldMatch = 1
        }
    };

        // 如果有错误时间，就做时间窗口（关键🔥）
        if (errorTime.HasValue)
        {
            var from = errorTime.Value.AddSeconds(-windowSeconds);
            var to = errorTime.Value.AddSeconds(windowSeconds);

            filter.Add(new DateRangeQuery
            {
                Field = "@timestamp",
                Gte = from,
                Lte = to
            });
        }

        var request = new SearchRequest<ElasticLogDocument>(IndexPattern)
        {
            Size = take,
            Sort = new List<SortOptions>
        {
            new SortOptions
            {
                Field = new FieldSort
                {
                    Field = "@timestamp",
                    Order = SortOrder.Asc
                }
            }
        },
            Query = new BoolQuery
            {
                Must = must,
                Filter = filter
            }
        };

        var response = await _elasticClient.SearchAsync<ElasticLogDocument>(request);

        if (!response.IsValidResponse)
            throw new Exception(response.DebugInformation);

        return response.Hits
            .Select(x => x.Source)
            .Where(x => x != null)
            .ToList()!;
    }

    async Task<List<TraceLogEntry>> ILogService.GetAiErrorContextAsync(
        string traceId,
        DateTime? errorTime,
        int windowSeconds,
        int take)
    {
        var results = await GetAiErrorContextAsync(traceId, errorTime, windowSeconds, take);
        return results.Select(doc => new TraceLogEntry
        {
            Timestamp = doc.Timestamp,
            Level = doc.Level ?? "",
            Message = doc.Message ?? "",
            Exception = doc.Exception,
            Service = doc.Service
        }).ToList();
    }
}

public class ElasticLogDocument
{
    [System.Text.Json.Serialization.JsonPropertyName("@timestamp")]
    public DateTime Timestamp { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("level")]
    public string? Level { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("message")]
    public string? Message { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("exception")]
    public string? Exception { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("service")]
    public string? Service { get; set; }
}
