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
       
        [DisplayName("高度Pin1X")]  public string? PIN1X { get; set; }       
        [DisplayName("高度Pin2X")]  public string? PIN2X { get; set; }     
        [DisplayName("高度Pin3X")]  public string? PIN3X { get; set; }
      
        [DisplayName("高度Pin1Y")]  public string? PIN1Y { get; set; }     
        [DisplayName("高度Pin2Y")]  public string? PIN2Y { get; set; }
        [DisplayName("高度Pin3Y")]  public string? PIN3Y { get; set; }

        public Brush? Pin1Color { get; set; }=Brushes.Red;
        //用于日志记录
        [SugarColumn(IsIgnore = true)]
        public string? Message { get; set; } 
    }
}
