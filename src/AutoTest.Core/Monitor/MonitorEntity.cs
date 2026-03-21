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

        private readonly List<IAssertion> _assertions = new();
        public IReadOnlyCollection<IAssertion> Assertions => _assertions;

        public MonitorEntity(Guid id, string name, MonitorTarget target)
        {
            Id = id;
            Name = name;
            Target = target ?? throw new ArgumentNullException(nameof(target));
        }

        public void AddAssertion(IAssertion assertion)
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

    }
}
