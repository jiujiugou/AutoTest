using System.Data;

namespace AutoTest.Infrastructure;

public interface IDbConnectionFactory
{
    IDbConnection CreateConnection();
}
