using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LayoutDemo
{

    /// <summary>
    /// 布局模式枚举
    /// </summary>
    public enum LayoutMode
    {
        // 垂直布局 - 所有区域纵向排列
        Vertical,

        // 水平布局 - 所有区域横向排列
        Horizontal,

        // 网格布局 - 田字格形状布局
        Grid,

        // 上部区域一格占满，下部分为左右两个区域
        TopSpan,

        // 下部区域一格占满，上部分为左右两个区域
        BottomSpan,

        // 左部区域一格占满，右部分为上下两个区域
        LeftSpan,

        // 右部区域一格占满，左部分为上下两个区域
        RightSpan,

        // 自定义布局 - 支持多种灵活布局方式
        Custom
    }

    /// <summary>
    /// 布局位置枚举
    /// </summary>
    public enum LayoutPosition
    {
        // 顶部位置
        Top,

        // 底部位置
        Bottom,

        // 左侧位置
        Left,

        // 右侧位置
        Right,

        // 中心位置
        Center,

        // 左上位置
        TopLeft,

        // 右上位置
        TopRight,

        // 左下位置
        BottomLeft,

        // 右下位置
        BottomRight,

        // 未指定位置(不显示)
        None
    }


}
