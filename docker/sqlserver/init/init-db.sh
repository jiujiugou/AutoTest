#!/usr/bin/env bash
set -euo pipefail

SQLSERVER_HOST="${SQLSERVER_HOST:-sqlserver}"
SA_PASSWORD="${SA_PASSWORD:-}"
AUTOTEST_DB="${AUTOTEST_DB:-AutoTestDb}"
HANGFIRE_DB="${HANGFIRE_DB:-HangfireDb}"
SQLCMD="/opt/mssql-tools18/bin/sqlcmd"

if [ ! -x "$SQLCMD" ]; then
  SQLCMD="/opt/mssql-tools/bin/sqlcmd"
fi

if [ ! -x "$SQLCMD" ]; then
  echo "sqlcmd not found in container." >&2
  exit 1
fi

if [ -z "$SA_PASSWORD" ]; then
  echo "SA_PASSWORD is required." >&2
  exit 1
fi

echo "Waiting for SQL Server at ${SQLSERVER_HOST}:1433 ..."

for i in $(seq 1 60); do
  if "$SQLCMD" -S "$SQLSERVER_HOST" -U sa -P "$SA_PASSWORD" -C -Q "SELECT 1" >/dev/null 2>&1; then
    echo "SQL Server is ready."
    break
  fi

  if [ "$i" -eq 60 ]; then
    echo "SQL Server did not become ready in time." >&2
    exit 1
  fi

  sleep 2
done

"$SQLCMD" -S "$SQLSERVER_HOST" -U sa -P "$SA_PASSWORD" -C -Q "IF DB_ID(N'${AUTOTEST_DB}') IS NULL CREATE DATABASE [${AUTOTEST_DB}];"
"$SQLCMD" -S "$SQLSERVER_HOST" -U sa -P "$SA_PASSWORD" -C -Q "IF DB_ID(N'${HANGFIRE_DB}') IS NULL CREATE DATABASE [${HANGFIRE_DB}];"

echo "Database initialization completed."
