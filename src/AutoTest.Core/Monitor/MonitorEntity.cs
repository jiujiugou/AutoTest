using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AutoTest.Core.Assertion;

namespace AutoTest.Core
{
    /// <summary>
    /// 监控任务的核心实体。
    /// </summary>
    /// <remarks>
    /// 描述一个可执行的监控任务：包含目标 <see cref="MonitorTarget"/>、运行状态、调度配置以及断言规则集合。
    /// </remarks>
    public class MonitorEntity : IAggregateRoot
    {
        /// <summary>
        /// 监控任务 ID。
        /// </summary>
        public Guid Id { get; private set; }

        /// <summary>
        /// 监控任务名称。
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// 监控目标配置。
        /// </summary>
        public MonitorTarget Target { get; private set; }

        /// <summary>
        /// 当前任务状态。
        /// </summary>
        public MonitorStatus Status { get; private set; } = MonitorStatus.Pending;

        /// <summary>
        /// 最近一次开始执行的时间（UTC）。
        /// </summary>
        public DateTime? LastRunTime { get; private set; }

        /// <summary>
        /// 是否启用该任务。
        /// </summary>
        public bool IsEnabled { get; private set; } = true;

        /// <summary>
        /// 是否启用每日自动执行。
        /// </summary>
        public bool AutoDailyEnabled { get; private set; }

        /// <summary>
        /// 每日自动执行时间（字符串形式，便于与前端交互）。
        /// </summary>
        public string? AutoDailyTime { get; private set; }

        /// <summary>
        /// 最大执行次数限制（为空表示不限制）。
        /// </summary>
        public int? MaxRuns { get; private set; }

        /// <summary>
        /// 已执行次数（用于限次逻辑）。
        /// </summary>
        public int ExecutedCount { get; private set; }
        private readonly List<AssertionRule> _assertions = new();

        /// <summary>
        /// 断言规则集合（只读视图）。
        /// </summary>
        public IReadOnlyCollection<AssertionRule> Assertions => _assertions;
        private MonitorEntity()
        {
            Name = null!;
            Target = null!;
        } // For ORM

        /// <summary>
        /// 创建监控任务。
        /// </summary>
        public MonitorEntity(
            Guid id,
            string name,
            MonitorTarget target,
            MonitorStatus status,
            DateTime? lastRunTime,
            bool isEnabled = true,
            bool autoDailyEnabled = false,
            string? autoDailyTime = null,
            int? maxRuns = null,
            int executedCount = 0)
        {
            Id = id;
            Name = name;
            Target = target ?? throw new ArgumentNullException(nameof(target));
            Status = status;
            LastRunTime = lastRunTime;
            IsEnabled = isEnabled;
            AutoDailyEnabled = autoDailyEnabled;
            AutoDailyTime = autoDailyTime;
            MaxRuns = maxRuns;
            ExecutedCount = executedCount;
        }

        /// <summary>
        /// 更新任务基础信息。
        /// </summary>
        public void Update(string name, MonitorTarget target, bool isEnabled)
        {
            Name = name;
            Target = target;
            IsEnabled = isEnabled;
        }

        /// <summary>
        /// 更新调度配置。
        /// </summary>
        public void UpdateSchedule(bool autoDailyEnabled, string? autoDailyTime, int? maxRuns)
        {
            AutoDailyEnabled = autoDailyEnabled;
            AutoDailyTime = autoDailyTime;
            MaxRuns = maxRuns;
        }

        /// <summary>
        /// 设置已执行次数（通常由持久化层统计后回填）。
        /// </summary>
        public void SetExecutedCount(int executedCount)
        {
            ExecutedCount = executedCount;
        }
        // ======================
        // 状态控制（核心）
        // ======================

        public bool CanExecute()
        {
            return IsEnabled && Status != MonitorStatus.Running;
        }

        /// <summary>
        /// 标记任务进入 Running 状态，并记录本次开始执行时间。
        /// </summary>
        public void MarkRunning()
        {
            if (!CanExecute())
                throw new InvalidOperationException("Monitor cannot execute");

            Status = MonitorStatus.Running;
            LastRunTime = DateTime.UtcNow;
        }

        /// <summary>
        /// 标记任务执行成功（从 Running 转为 Success）。
        /// </summary>
        public void MarkSuccess()
        {
            if (Status != MonitorStatus.Running)
                throw new InvalidOperationException("Invalid state transition");

            Status = MonitorStatus.Success;
        }

        /// <summary>
        /// 标记任务执行失败（从 Running 转为 Failed）。
        /// </summary>
        public void MarkFailed()
        {
            if (Status != MonitorStatus.Running)
                throw new InvalidOperationException("Invalid state transition");

            Status = MonitorStatus.Failed;
        }

        public void Enable() => IsEnabled = true;
        public void Disable() => IsEnabled = false;

        // ======================
        // Assertion 管理
        // ======================

        /// <summary>
        /// 添加断言规则。
        /// </summary>
        public void AddAssertion(AssertionRule assertion)
        {
            if (_assertions.Any(a => a.Id == assertion.Id))
                throw new InvalidOperationException("Duplicate assertion");

            _assertions.Add(assertion);
        }

        /// <summary>
        /// 移除断言规则。
        /// </summary>
        public void RemoveAssertion(Guid assertionId)
        {
            var assertion = _assertions.FirstOrDefault(a => a.Id == assertionId);
            if (assertion != null)
                _assertions.Remove(assertion);
        }

        /// <summary>
        /// 清空所有断言规则。
        /// </summary>
        public void ClearAssertions() => _assertions.Clear();
    }
}
