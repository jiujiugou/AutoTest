using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AutoTest.Core.Log;

namespace AutoTest.Core
{
    public interface IExecutionLogRepository
    {
        void AddLog(ExecutionLog log);
        IEnumerable<ExecutionLog> GetLogsByMonitorId(Guid monitorId);
        IEnumerable<ExecutionLog> GetAllLogs();
    }
}
