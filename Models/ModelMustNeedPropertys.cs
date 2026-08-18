using SqlSugar;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfApp1.Models
{
    /// <summary>
    /// 所有类模型类必须继承该类，保证数据库表的基本属性
    /// </summary>
    public class ModelMustNeedPropertys
    {
        /// <summary>
        /// 则增加主键id，数据库自增
        /// </summary>
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int id { get; set; }
        [SugarColumn(IsNullable = false)]
        //测试时间
        [DisplayName("时间")]
        public DateTime DateTime { get; set; }
        //产品名称
        [DisplayName("产品名称")]
        public string ProductionName { get; set; }
        //测试结果
        [DisplayName("结果")]
        public bool EndResult { get; set; }
    }
}
