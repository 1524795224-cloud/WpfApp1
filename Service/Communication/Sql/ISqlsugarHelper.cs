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
        /// <summary>
        /// 创建数据库（如果不存在）
        /// </summary>
        void CreateDatabase();

        /// <summary>
        /// 创建数据表（根据实体类型）
        /// </summary>
        /// <param name="entityTypes">实体类型数组</param>
        void CreateTables(params Type[] entityTypes);

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
        /// 获取原生 SqlSugarClient 实例，用于执行更复杂的操作
        /// </summary>
        SqlSugarClient GetClient();
    }
}