using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AutoTest.Core.Assertion;

namespace AutoTest.Core
{
    //业务实体规则,核心实体
    public class MonitorEntity : IAggregateRoot
    {
        public Guid Id { get; private set; }
        public string Name { get; private set; }
        public MonitorTarget Target { get; private set; }
        public MonitorStatus Status { get; private set; } = MonitorStatus.Pending;
        public DateTime? LastRunTime { get; private set; }
        public bool IsEnabled { get; private set; } = true;
        private readonly List<AssertionRule> _assertions = new();
        public IReadOnlyCollection<AssertionRule> Assertions => _assertions;
        private MonitorEntity() { } // For ORM
        public MonitorEntity(Guid id, string name, MonitorTarget target, MonitorStatus status, DateTime? lastRunTime, bool isEnabled = true)
        {
            Id = id;
            Name = name;
            Target = target ?? throw new ArgumentNullException(nameof(target));
            Status = status;
            LastRunTime = lastRunTime;
            IsEnabled = isEnabled;
        }
        public void Update(string name, MonitorTarget target, bool isEnabled)
        {
            Name = name;
            Target = target;
            IsEnabled = isEnabled;
        }
        // ======================
        // 状态控制（核心）
        // ======================

        public bool CanExecute()
        {
            return IsEnabled && Status != MonitorStatus.Running;
        }

        public void MarkRunning()
        {
            if (!CanExecute())
                throw new InvalidOperationException("Monitor cannot execute");

            Status = MonitorStatus.Running;
            LastRunTime = DateTime.UtcNow;
        }

        public void MarkSuccess()
        {
            if (Status != MonitorStatus.Running)
                throw new InvalidOperationException("Invalid state transition");

            Status = MonitorStatus.Success;
        }

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

        public void AddAssertion(AssertionRule assertion)
        {
            if (_assertions.Any(a => a.Id == assertion.Id))
                throw new InvalidOperationException("Duplicate assertion");

            _assertions.Add(assertion);
        }

        public void RemoveAssertion(Guid assertionId)
        {
            var assertion = _assertions.FirstOrDefault(a => a.Id == assertionId);
            if (assertion != null)
                _assertions.Remove(assertion);
        }

        public void ClearAssertions() => _assertions.Clear();
    }
}
