
//------------------------------------------------------------------------------
// <auto-generated>
//    Genera by BDFramework
// </auto-generated>
//------------------------------------------------------------------------------

namespace Game.Data.Local
{
    using System;
    using System.Collections.Generic;
    using SQLite4Unity3d;
    
    [Serializable()]
    public class BuffTest
    {
        
        /// <summary>
        /// buffid
        /// </summary>
        [PrimaryKey]
        public int Id {get;set;}
        /// <summary>
        /// buff类型
        /// </summary>
        public int BuffType {get;set;}
        /// <summary>
        /// 冷却时间（回合）
        /// </summary>
        public int CD {get;set;}
        /// <summary>
        /// 持续时间（回合）
        /// </summary>
        public int LifeTime {get;set;}
        /// <summary>
        /// 描述
        /// </summary>
        public string Des {get;set;}
        /// <summary>
        /// 参数列表，字符串类型
        /// </summary>
        public string[] Params_StrValue {get;set;}
        /// <summary>
        /// 公式
        /// </summary>
        public string[] Params_Expression {get;set;}
        /// <summary>
        /// 参数列表，数值类型(固定数值)
        /// </summary>
        public int[] Params_NumValue {get;set;}
        /// <summary>
        /// 显示特效
        /// </summary>
        public string Effect {get;set;}
    }
}