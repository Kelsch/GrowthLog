using System.Data;

namespace GrowthLog.Web.Data;

public interface IDbConnectionFactory
{
    IDbConnection Create();
}
