using System.ComponentModel;


namespace WpfApp1
{
    public static class GlobalCs
    {
    }
    /// <summary>
    /// 系统状态，全局的
    /// </summary>
    public static class SystemStaton
    {
        //项目名称
        public static string? ProgramName { get; set; }
        //产品名称
        public static string? ProductionName { get; set; }
        //当前模号
        public static string? Model { get; set; }
        //是否是管理员，是的话，可以更改参数,默认是false
        public static bool IsAdmin { get; set; } = false;
    }
    /// <summary>
    /// 方法最后的结果
    /// </summary>
    public enum OutCome
    {
        [Description("成功")]
        Success,
        [Description("失败")]
        Fail
    }
}
