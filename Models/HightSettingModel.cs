using SqlSugar;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Media;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfApp1.Models
{
    [SugarTable("HightSetting")]
    public class HightSettingModel:ModelMustNeedPropertys
    {
       
        [DisplayName("高度Pin1X")]
        [SugarColumn(Length =50, IsNullable = true)]
        public string? PIN1X { get; set; }       
        [DisplayName("高度Pin2X")][SugarColumn(Length = 50, IsNullable = true)] public string? PIN2X { get; set; }     
        [DisplayName("高度Pin3X")][SugarColumn(Length = 50, IsNullable = true)] public string? PIN3X { get; set; }
      
        [DisplayName("高度Pin1Y")][SugarColumn(Length = 50, IsNullable = true)] public string? PIN1Y { get; set; }     
        [DisplayName("高度Pin2Y")][SugarColumn(Length = 50, IsNullable = true)] public string? PIN2Y { get; set; }
        [DisplayName("高度Pin3Y")][SugarColumn(Length = 50, IsNullable = true)] public string? PIN3Y { get; set; }

       
        //用于日志记录
        [SugarColumn(IsIgnore = true)]
        public string? Message { get; set; } 
    }
}
