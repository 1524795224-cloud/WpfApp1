using System.Linq.Expressions;
using SqlSugar;

namespace WpfApp1.Service.Communication.Sql
{
    /// <summary>
    /// SqlSugar 数据库帮助类
    /// 负责创建数据库、数据表，提供常用的插入和查询操作
    /// </summary>
    public class SqlsugarHelper: ISqlsugarHelper
    {
        private SqlSugarClient _db;
        private readonly string _databaseName;
        private readonly DbType _dbType;
        private readonly string _connectionString;
        private readonly string _server;
        private readonly string _userId;
        private readonly string _password;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="databaseName">数据库名称</param>
        /// <param name="dbType">数据库类型，默认 SQL Server</param>
        /// <param name="server">服务器地址，默认本机 (.)</param>
        /// <param name="userId">用户名（SQL Server 身份验证时使用，Windows 身份验证可留空）</param>
        /// <param name="password">密码（SQL Server 身份验证时使用，Windows 身份验证可留空）</param>
        public SqlsugarHelper(string databaseName, DbType dbType = DbType.SqlServer, string server = ".", string userId = null, string password = null)
        {
            _databaseName = databaseName;
            _dbType = dbType;
            _server = server;
            _userId = userId;
            _password = password;

            // 根据数据库类型构建连接字符串
            _connectionString = BuildConnectionString(databaseName, server, userId, password);

            // 初始化 SqlSugar 客户端
            _db = new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = _connectionString,
                DbType = _dbType,
                IsAutoCloseConnection = true
            });
        }

        /// <summary>
        /// 创建数据库（如果不存在）
        /// 注意：创建数据库后需要重新初始化连接
        /// </summary>
        public void CreateDatabase()
        {
            try
            {
                if (_dbType == DbType.SqlServer)
                {
                    // 对于 SQL Server，需要先连接到 master 数据库才能创建新数据库
                    string masterConnectionString = BuildMasterConnectionString();
                    using (var masterDb = new SqlSugarClient(new ConnectionConfig
                    {
                        ConnectionString = masterConnectionString,
                        DbType = _dbType,
                        IsAutoCloseConnection = true
                    }))
                    {
                        // 判断数据库是否存在
                        string checkSql = "SELECT COUNT(1) FROM master.dbo.sysdatabases WHERE name = @dbName";
                        var param = new SugarParameter("@dbName", _databaseName);
                        int exists = masterDb.Ado.GetInt(checkSql, param);

                        if (exists == 0)
                        {
                            // 创建数据库（数据库名加方括号防止注入）
                            string createSql = $"CREATE DATABASE [{_databaseName}]";
                            masterDb.Ado.ExecuteCommand(createSql);
                        }
                    }
                }
                else if (_dbType == DbType.MySql)
                {
                    // MySQL：连接时可以不指定数据库，直接连服务器
                    string serverConnectionString = BuildServerConnectionString();
                    using (var serverDb = new SqlSugarClient(new ConnectionConfig
                    {
                        ConnectionString = serverConnectionString,
                        DbType = _dbType,
                        IsAutoCloseConnection = true
                    }))
                    {
                        string checkSql = "SELECT COUNT(1) FROM INFORMATION_SCHEMA.SCHEMATA WHERE SCHEMA_NAME = @dbName";
                        var param = new SugarParameter("@dbName", _databaseName);
                        int exists = serverDb.Ado.GetInt(checkSql, param);

                        if (exists == 0)
                        {
                            string createSql = $"CREATE DATABASE `{_databaseName}`";
                            serverDb.Ado.ExecuteCommand(createSql);
                        }
                    }
                }
                else
                {
                    // 其他数据库（如 SQLite 不需要创建数据库，文件即数据库）
                    throw new NotSupportedException($"当前数据库类型 {_dbType} 的自动创建数据库功能未实现，请手动创建或使用适合的连接字符串。");
                }

                // 重新初始化 SqlSugarClient，连接到新创建的数据库
                _db = new SqlSugarClient(new ConnectionConfig
                {
                    ConnectionString = _connectionString,
                    DbType = _dbType,
                    IsAutoCloseConnection = true
                });
            }
            catch (Exception ex)
            {
                throw new Exception($"创建数据库失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 创建数据表（根据实体类型）
        /// </summary>
        /// <param name="entityTypes">实体类型数组，例如 typeof(User), typeof(Product)</param>
        public void CreateTables(params Type[] entityTypes)
        {
            if (entityTypes == null || entityTypes.Length == 0)
            {
                throw new ArgumentException("请至少提供一个实体类型");
            }

            try
            {
                _db.CodeFirst.InitTables(entityTypes);
            }
            catch (Exception ex)
            {
                throw new Exception($"创建数据表失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 插入单个实体
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="entity">要插入的实体对象</param>
        /// <returns>受影响的行数</returns>
        public int Insert<T>(T entity) where T : class, new()
        {
            return _db.Insertable(entity).ExecuteCommand();
        }

        /// <summary>
        /// 插入单个实体并返回自增ID
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="entity">要插入的实体对象</param>
        /// <returns>自增ID值</returns>
        public int InsertReturnIdentity<T>(T entity) where T : class, new()
        {
            return _db.Insertable(entity).ExecuteReturnIdentity();
        }

        /// <summary>
        /// 批量插入实体列表
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="entities">实体列表</param>
        /// <returns>受影响的行数</returns>
        public int InsertRange<T>(List<T> entities) where T : class, new()
        {
            return _db.Insertable(entities).ExecuteCommand();
        }

        /// <summary>
        /// 查询所有数据
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <returns>实体列表</returns>
        public List<T> QueryAll<T>() where T : class, new()
        {
            return _db.Queryable<T>().ToList();
        }

        /// <summary>
        /// 根据条件查询数据
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="predicate">查询条件表达式，例如 it => it.Id > 10</param>
        /// <returns>实体列表</returns>
        public List<T> QueryByCondition<T>(Expression<Func<T, bool>> predicate) where T : class, new()
        {
            return _db.Queryable<T>().Where(predicate).ToList();
        }

        /// <summary>
        /// 查询第一条数据（无结果返回 null）
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="predicate">查询条件表达式</param>
        /// <returns>实体对象或 null</returns>
        public T QueryFirst<T>(Expression<Func<T, bool>> predicate) where T : class, new()
        {
            return _db.Queryable<T>().Where(predicate).First();
        }

        /// <summary>
        /// 获取原生 SqlSugarClient 实例，用于执行更复杂的操作
        /// </summary>
        public SqlSugarClient GetClient()
        {
            return _db;
        }

        #region 私有方法

        /// <summary>
        /// 构建连接字符串
        /// </summary>
        private string BuildConnectionString(string databaseName, string server, string userId, string password)
        {
            switch (_dbType)
            {
                case DbType.SqlServer:
                    if (string.IsNullOrEmpty(userId))
                    {
                        // Windows 身份验证
                        return $"Data Source={server};Initial Catalog={databaseName};Integrated Security=True;TrustServerCertificate=True;";
                    }
                    else
                    {
                        // SQL Server 身份验证
                        return $"Data Source={server};Initial Catalog={databaseName};User ID={userId};Password={password};TrustServerCertificate=True;";
                    }
                case DbType.MySql:
                    return $"Server={server};Database={databaseName};Uid={userId ?? "root"};Pwd={password ?? ""};";
                case DbType.Sqlite:
                    return $"Data Source={databaseName}.db;"; // SQLite 数据库名作为文件名
                default:
                    throw new NotSupportedException($"暂不支持数据库类型: {_dbType}");
            }
        }

        /// <summary>
        /// 构建用于创建数据库的 master 连接字符串（仅 SQL Server）
        /// </summary>
        private string BuildMasterConnectionString()
        {
            // 默认使用 Windows 身份验证连接 master，如果使用 SQL Server 身份验证，请自行修改
            if (string.IsNullOrEmpty(_userId))
            {
                return $"Data Source={_server};Initial Catalog=master;Integrated Security=True;TrustServerCertificate=True;";
            }
            else
            {
                return $"Data Source={_server};Initial Catalog=master;User ID={_userId};Password={_password};TrustServerCertificate=True;";
            }
        }

        /// <summary>
        /// 构建不指定数据库的连接字符串（用于 MySQL 创建数据库）
        /// </summary>
        private string BuildServerConnectionString()
        {
            if (_dbType == DbType.MySql)
            {
                // 直接使用服务器、用户名、密码，不包含 Database
                return $"Server={_server};Uid={_userId ?? "root"};Pwd={_password ?? ""};";
            }
            throw new NotSupportedException("仅支持 MySQL 获取服务器连接字符串");
        }

        #endregion
    }
}