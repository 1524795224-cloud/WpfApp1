using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using SqlSugar;

namespace WpfApp1.Service.Communication.Sql
{
    /// <summary>
    /// SqlSugar 数据库帮助类接口
    /// </summary>
    public interface ISqlsugarHelper
    {
        #region 数据库与表初始化

        /// <summary>
        /// 创建数据库（如果不存在）
        /// </summary>
        void CreateDatabase();

        /// <summary>
        /// 创建数据表（根据实体类型）
        /// </summary>
        /// <param name="entityTypes">实体类型数组，例如 typeof(User), typeof(Product)</param>
        void CreateTables(params Type[] entityTypes);

        #endregion

        #region 写入操作 (Create)

        /// <summary>
        /// 插入单个实体
        /// </summary>
        int Insert<T>(T entity) where T : class, new();

        /// <summary>
        /// 插入单个实体并返回自增ID
        /// </summary>
        int InsertReturnIdentity<T>(T entity) where T : class, new();

        /// <summary>
        /// 批量插入实体列表
        /// </summary>
        int InsertRange<T>(List<T> entities) where T : class, new();

        #endregion

        #region 更新与删除操作 (Update & Delete)

        /// <summary>
        /// 更新单个实体
        /// </summary>
        bool Update<T>(T entity) where T : class, new();

        /// <summary>
        /// 根据条件删除数据
        /// </summary>
        bool Delete<T>(Expression<Func<T, bool>> predicate) where T : class, new();

        #endregion

        #region 查询操作 (Read)

        /// <summary>
        /// 查询所有数据
        /// </summary>
        List<T> QueryAll<T>() where T : class, new();

        /// <summary>
        /// 根据条件查询数据
        /// </summary>
        List<T> QueryByCondition<T>(Expression<Func<T, bool>> predicate) where T : class, new();

        /// <summary>
        /// 查询第一条数据（无结果返回 null）
        /// </summary>
        T QueryFirst<T>(Expression<Func<T, bool>> predicate) where T : class, new();

        /// <summary>
        /// 根据 Type 动态查询所有数据（专供 UserControl 用户控件绑定使用）
        /// </summary>
        /// <param name="entityType">实体 Model 的 Type</param>
        object QueryListByEntityType(Type entityType);

        /// <summary>
        /// 通用条件分页查询
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="pageIndex">当前页码（从 1 开始）</param>
        /// <param name="pageSize">每页展示条数</param>
        /// <param name="totalCount">返回查到的总记录数</param>
        /// <param name="predicate">可选条件过滤表达式</param>
        List<T> QueryPage<T>(int pageIndex, int pageSize, ref int totalCount, Expression<Func<T, bool>>? predicate = null) where T : class, new();

        #endregion

        #region 底层客户端获取

        /// <summary>
        /// 获取原生 ISqlSugarClient 实例，用于执行更复杂的联表或事务操作
        /// </summary>
        ISqlSugarClient GetClient();

        #endregion
    }
}