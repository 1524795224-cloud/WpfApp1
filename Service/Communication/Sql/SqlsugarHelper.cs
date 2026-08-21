using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using SqlSugar;

namespace WpfApp1.Service.Communication.Sql
{
    /// <summary>
    /// SqlSugar 数据库帮助类
    /// 负责创建数据库、数据表，提供常用的增删改查操作
    /// </summary>
    public class SqlsugarHelper : ISqlsugarHelper
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
        public SqlsugarHelper(string databaseName, DbType dbType = DbType.SqlServer, string server = ".", string userId = null, string password = null)
        {
            _databaseName = databaseName;
            _dbType = dbType;
            _server = server;
            _userId = userId;
            _password = password;

            _connectionString = BuildConnectionString(databaseName, server, userId, password);

            // 初始化并配置全局字段映射规则
            _db = CreateClientInstance(_connectionString);
        }

        /// <summary>
        /// 创建并配置带有全局规则（如 NVARCHAR 长度）的 SqlSugarClient
        /// </summary>
        private SqlSugarClient CreateClientInstance(string connectionString)
        {
            return new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = connectionString,
                DbType = _dbType,
                IsAutoCloseConnection = true,
                ConfigureExternalServices = new ConfigureExternalServices
                {
                    // 全局实体属性服务：未设置 Length 属性的 string 类型默认映射为 NVARCHAR(50)
                    EntityService = (property, column) =>
                    {
                        if (property.PropertyType == typeof(string) && column.Length == 0)
                        {
                            column.Length = 50;
                        }
                    }
                }
            });
        }

        /// <summary>
        /// 创建数据库（如果不存在）
        /// </summary>
        public void CreateDatabase()
        {
            try
            {
                if (_dbType == DbType.SqlServer)
                {
                    string masterConnectionString = BuildMasterConnectionString();
                    using (var masterDb = CreateClientInstance(masterConnectionString))
                    {
                        string checkSql = "SELECT COUNT(1) FROM master.dbo.sysdatabases WHERE name = @dbName";
                        var param = new SugarParameter("@dbName", _databaseName);
                        int exists = masterDb.Ado.GetInt(checkSql, param);

                        if (exists == 0)
                        {
                            string createSql = $"CREATE DATABASE [{_databaseName}]";
                            masterDb.Ado.ExecuteCommand(createSql);
                        }
                    }
                }
                else if (_dbType == DbType.MySql)
                {
                    string serverConnectionString = BuildServerConnectionString();
                    using (var serverDb = CreateClientInstance(serverConnectionString))
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
                    App.Logger.Database($"当前数据库类型 {_dbType} 的自动创建数据库功能未实现，请手动创建或使用适合的连接字符串。");
                }

                // 重新初始化，连接到新建的数据库
                _db = CreateClientInstance(_connectionString);
            }
            catch (Exception ex)
            {
                App.Logger.Error($"创建数据库失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 根据实体类型初始化创建数据表
        /// </summary>
        public void CreateTables(params Type[] entityTypes)
        {
            if (entityTypes == null || entityTypes.Length == 0)
            {
                App.Logger.Database("请至少提供一个实体类型");
            }

            try
            {
                _db.CodeFirst.InitTables(entityTypes);
            }
            catch (Exception ex)
            {
                App.Logger.Error($"创建数据表失败: {ex.Message}", ex);
            }
        }

        #region 写入操作 (Insert)

        public int Insert<T>(T entity) where T : class, new()
        {
            return _db.Insertable(entity).ExecuteCommand();
        }

        public int InsertReturnIdentity<T>(T entity) where T : class, new()
        {
            return _db.Insertable(entity).ExecuteReturnIdentity();
        }

        public int InsertRange<T>(List<T> entities) where T : class, new()
        {
            return _db.Insertable(entities).ExecuteCommand();
        }

        #endregion

        #region 更新与删除操作 (Update & Delete)

        public bool Update<T>(T entity) where T : class, new()
        {
            return _db.Updateable(entity).ExecuteCommand() > 0;
        }

        public bool Delete<T>(Expression<Func<T, bool>> predicate) where T : class, new()
        {
            return _db.Deleteable<T>().Where(predicate).ExecuteCommand() > 0;
        }

        #endregion

        #region 查询操作 (Query)

        public List<T> QueryAll<T>() where T : class, new()
        {
            return _db.Queryable<T>().ToList();
        }

        public List<T> QueryByCondition<T>(Expression<Func<T, bool>> predicate) where T : class, new()
        {
            return _db.Queryable<T>().Where(predicate).ToList();
        }

        public T QueryFirst<T>(Expression<Func<T, bool>> predicate) where T : class, new()
        {
            return _db.Queryable<T>().Where(predicate).First();
        }

        /// <summary>
        /// 核心：通过 Type 类型动态从对应的表中读取数据列表（UserControl 使用）
        /// </summary>
        public object QueryListByEntityType(Type entityType)
        {
            return _db.QueryableByObject(entityType).ToList();
        }

        /// <summary>
        /// 通用条件分页查询
        /// </summary>
        public List<T> QueryPage<T>(int pageIndex, int pageSize, ref int totalCount, Expression<Func<T, bool>>? predicate = null) where T : class, new()
        {
            var query = _db.Queryable<T>();
            if (predicate != null)
            {
                query = query.Where(predicate);
            }
            return query.ToPageList(pageIndex, pageSize, ref totalCount);
        }

        #endregion

        #region 客户端实例获取

        public ISqlSugarClient GetClient()
        {
            return _db;
        }

        #endregion

        #region 私有核心辅助逻辑

        private string BuildConnectionString(string databaseName, string server, string userId, string password)
        {
            switch (_dbType)
            {
                case DbType.SqlServer:
                    if (string.IsNullOrEmpty(userId))
                    {
                        return $"Data Source={server};Initial Catalog={databaseName};Integrated Security=True;TrustServerCertificate=True;";
                    }
                    else
                    {
                        return $"Data Source={server};Initial Catalog={databaseName};User ID={userId};Password={password};TrustServerCertificate=True;";
                    }
                case DbType.MySql:
                    return $"Server={server};Database={databaseName};Uid={userId ?? "root"};Pwd={password ?? ""};";
                case DbType.Sqlite:
                    return $"Data Source={databaseName}.db;";
                default:
                    throw new NotSupportedException($"暂不支持数据库类型: {_dbType}");
            }
        }

        private string BuildMasterConnectionString()
        {
            if (string.IsNullOrEmpty(_userId))
            {
                return $"Data Source={_server};Initial Catalog=master;Integrated Security=True;TrustServerCertificate=True;";
            }
            else
            {
                return $"Data Source={_server};Initial Catalog=master;User ID={_userId};Password={_password};TrustServerCertificate=True;";
            }
        }

        private string BuildServerConnectionString()
        {
            if (_dbType == DbType.MySql)
            {
                return $"Server={_server};Uid={_userId ?? "root"};Pwd={_password ?? ""};";
            }
            throw new NotSupportedException("仅支持 MySQL 获取服务器连接字符串");
        }

        #endregion
    }
}