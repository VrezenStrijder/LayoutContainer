using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms.Design;
using System.Windows.Forms;
using System.Collections;
using System.Diagnostics;
using LayoutDemo.Controls.Design;

namespace LayoutDemo
{
    /// <summary>
    /// 自定义布局容器控件 - 支持1至4格灵活布局
    /// </summary>
    [Designer(typeof(LayoutContainerDesigner))]
    [ToolboxItem(true)]
    [DefaultProperty("LayoutModeValue")]
    [DefaultEvent("LayoutModeChanged")]
    public class LayoutContainer : Control
    {
        #region 私有字段

        private LayoutMode layoutMode = LayoutMode.Vertical;
        private readonly GridCell[] gridCells = new GridCell[4];
        private float splitterSize = 2;
        private float horizontalSplitterDistance = 0.25f; // 水平拆分距离(0-1)
        private float verticalSplitterDistance = 0.25f;   // 垂直拆分距离(0-1)

        private float secondHorizontalSplitterDistance = 0.75f; // 第二个水平分隔条位置
        private float secondVerticalSplitterDistance = 0.75f;   // 第二个垂直分隔条位置

        private bool isDragging = false;
        private Point dragStartPoint;
        private int activeSplitter = -1;


        private bool isResizing = false;
        private int resizingEdge = -1; // 0=上, 1=右, 2=下, 3=左
        private Point lastMousePosition;
        private const int RESIZE_BORDER_WIDTH = 5; // 拖动边缘的宽度(像素)


        // 分隔条覆盖层对象
        //private SplitterLayer _splitterOverlay = null;
        private bool showBorder = true;
        private int borderSize = 1;
        private Color borderColor = Color.DarkGray;


        // 格子可见性控制
        private bool showTopPanel = true;
        private bool showBottomPanel = true;
        private bool showLeftPanel = true;
        private bool showRightPanel = true;

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化 LayoutContainer 控件的新实例
        /// </summary>
        public LayoutContainer()
        {
            // 初始化基础设置
            SetStyle(ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.DoubleBuffer, true);
            SetStyle(ControlStyles.AllPaintingInWmPaint, true);
            SetStyle(ControlStyles.UserPaint, true);
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            SetStyle(ControlStyles.Selectable, true);

            // 初始化格子容器
            for (int i = 0; i < 4; i++)
            {
                gridCells[i] = new GridCell(i + 1);
                gridCells[i].Tag = $"GridCell{i + 1}";  // 设置Tag以便在设计时识别
                gridCells[i].Visible = false;
                Controls.Add(gridCells[i]);
            }


            // 默认垂直布局
            UpdateLayoutPositions();

            // 启用用于大小调整的事件
            SetStyle(ControlStyles.Selectable, true);
        }

        #endregion

        #region 公共属性

        /// <summary>
        /// 获取或设置布局模式
        /// </summary>
        [Category("Appearance")]
        [Description("控件的布局模式")]
        [DefaultValue(LayoutMode.Vertical)]
        public LayoutMode LayoutModeValue
        {
            get => layoutMode;
            set
            {
                if (layoutMode != value)
                {
                    layoutMode = value;
                    UpdateLayoutPositions();
                    PerformLayout();
                    Invalidate();
                    OnLayoutModeChanged(EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// 获取或设置分隔条的大小
        /// </summary>
        [Category("Appearance")]
        [Description("分隔条的宽度或高度")]
        [DefaultValue(5.0f)]
        public float SplitterSize
        {
            get => splitterSize;
            set
            {
                if (splitterSize != value && value >= 1)
                {
                    splitterSize = value;
                    PerformLayout();
                    Invalidate();
                }
            }
        }

        /// <summary>
        /// 获取或设置水平分隔条的位置(百分比)
        /// </summary>
        [Category("Appearance")]
        [Description("水平分隔条的相对位置(0-1之间)")]
        [DefaultValue(0.5f)]
        public float HorizontalSplitterDistance
        {
            get => horizontalSplitterDistance;
            set
            {
                value = Math.Max(0.01f, Math.Min(0.9f, value));
                if (Math.Abs(horizontalSplitterDistance - value) > 0.001f)
                {
                    horizontalSplitterDistance = value;
                    if (horizontalSplitterDistance >= SecondHorizontalSplitterDistance)
                    {
                        secondHorizontalSplitterDistance = value + 0.05f;
                    }
                    PerformLayout();
                    Invalidate();
                }
            }
        }

        /// <summary>
        /// 获取或设置垂直分隔条的位置(百分比)
        /// </summary>
        [Category("Appearance")]
        [Description("垂直分隔条的相对位置(0-1之间)")]
        [DefaultValue(0.5f)]
        public float VerticalSplitterDistance
        {
            get => verticalSplitterDistance;
            set
            {
                value = Math.Max(0.01f, Math.Min(0.9f, value));
                if (Math.Abs(verticalSplitterDistance - value) > 0.001f)
                {
                    verticalSplitterDistance = value;
                    if (verticalSplitterDistance >= SecondVerticalSplitterDistance)
                    {
                        secondVerticalSplitterDistance = value + 0.05f;
                    }
                    PerformLayout();
                    Invalidate();
                }
            }
        }

        /// <summary>
        /// 获取或设置第二个水平分隔条的位置(百分比)
        /// </summary>
        [Category("Appearance")]
        [Description("第二个水平分隔条的相对位置(0-1之间)")]
        [DefaultValue(0.7f)]
        public float SecondHorizontalSplitterDistance
        {
            get => secondHorizontalSplitterDistance;
            set
            {
                value = Math.Max(horizontalSplitterDistance + 0.05f, Math.Min(0.99f, value));
                if (Math.Abs(secondHorizontalSplitterDistance - value) > 0.001f)
                {
                    secondHorizontalSplitterDistance = value;
                    PerformLayout();
                    Invalidate();
                }
            }
        }

        /// <summary>
        /// 获取或设置第二个垂直分隔条的位置(百分比)
        /// </summary>
        [Category("Appearance")]
        [Description("第二个垂直分隔条的相对位置(0-1之间)")]
        [DefaultValue(0.7f)]
        public float SecondVerticalSplitterDistance
        {
            get => secondVerticalSplitterDistance;
            set
            {
                value = Math.Max(verticalSplitterDistance + 0.05f, Math.Min(0.99f, value));
                if (Math.Abs(secondVerticalSplitterDistance - value) > 0.001f)
                {
                    secondVerticalSplitterDistance = value;
                    PerformLayout();
                    Invalidate();
                }
            }
        }


        /// <summary>
        /// 获取或设置顶部格子是否显示(垂直布局模式)
        /// </summary>
        [Category("Appearance")]
        [Description("顶部格子是否显示(仅垂直布局模式有效)")]
        [DefaultValue(true)]
        public bool ShowTopPanel
        {
            get => showTopPanel;
            set
            {
                if (showTopPanel != value)
                {
                    showTopPanel = value;
                    if (layoutMode == LayoutMode.Vertical)
                    {
                        UpdateLayoutPositions();
                        PerformLayout();
                        Invalidate();
                    }
                }
            }
        }

        /// <summary>
        /// 获取或设置底部格子是否显示(垂直布局模式)
        /// </summary>
        [Category("Appearance")]
        [Description("底部格子是否显示(仅垂直布局模式有效)")]
        [DefaultValue(true)]
        public bool ShowBottomPanel
        {
            get => showBottomPanel;
            set
            {
                if (showBottomPanel != value)
                {
                    showBottomPanel = value;
                    if (layoutMode == LayoutMode.Vertical)
                    {
                        UpdateLayoutPositions();
                        PerformLayout();
                        Invalidate();
                    }
                }
            }
        }

        /// <summary>
        /// 获取或设置左侧格子是否显示(水平布局模式)
        /// </summary>
        [Category("Appearance")]
        [Description("左侧格子是否显示(仅水平布局模式有效)")]
        [DefaultValue(true)]
        public bool ShowLeftPanel
        {
            get => showLeftPanel;
            set
            {
                if (showLeftPanel != value)
                {
                    showLeftPanel = value;
                    if (layoutMode == LayoutMode.Horizontal)
                    {
                        UpdateLayoutPositions();
                        PerformLayout();
                        Invalidate();
                    }
                }
            }
        }

        /// <summary>
        /// 获取或设置右侧格子是否显示(水平布局模式)
        /// </summary>
        [Category("Appearance")]
        [Description("右侧格子是否显示(仅水平布局模式有效)")]
        [DefaultValue(true)]
        public bool ShowRightPanel
        {
            get => showRightPanel;
            set
            {
                if (showRightPanel != value)
                {
                    showRightPanel = value;
                    if (layoutMode == LayoutMode.Horizontal)
                    {
                        UpdateLayoutPositions();
                        PerformLayout();
                        Invalidate();
                    }
                }
            }
        }

        /// <summary>
        /// 获取或设置是否显示边框
        /// </summary>
        [Category("Appearance")]
        [Description("是否显示控件边框")]
        [DefaultValue(false)]
        public bool ShowBorder
        {
            get => showBorder;
            set
            {
                if (showBorder != value)
                {
                    showBorder = value;
                    Invalidate();
                }
            }
        }

        /// <summary>
        /// 获取或设置边框大小
        /// </summary>
        [Category("Appearance")]
        [Description("控件边框的宽度")]
        [DefaultValue(1)]
        public int BorderSize
        {
            get => borderSize;
            set
            {
                if (borderSize != value && value >= 0)
                {
                    borderSize = value;
                    Invalidate();
                }
            }
        }

        /// <summary>
        /// 获取或设置边框颜色
        /// </summary>
        [Category("Appearance")]
        [Description("控件边框的颜色")]
        [DefaultValue(typeof(Color), "DarkGray")]
        public Color BorderColor
        {
            get => borderColor;
            set
            {
                if (borderColor != value)
                {
                    borderColor = value;
                    Invalidate();
                }
            }
        }


        /// <summary>
        /// 获取第一个格子
        /// </summary>
        [Browsable(false)]
        public GridCell Panel1 => gridCells[0];

        /// <summary>
        /// 获取第二个格子
        /// </summary>
        [Browsable(false)]
        public GridCell Panel2 => gridCells[1];

        /// <summary>
        /// 获取第三个格子
        /// </summary>
        [Browsable(false)]
        public GridCell Panel3 => gridCells[2];

        /// <summary>
        /// 获取第四个格子
        /// </summary>
        [Browsable(false)]
        public GridCell Panel4 => gridCells[3];

        /// <summary>
        /// 获取所有格子
        /// </summary>
        [Browsable(false)]
        internal GridCell[] GridCells => gridCells;

        #endregion

        #region 事件

        /// <summary>
        /// 布局模式变更事件
        /// </summary>
        public event EventHandler LayoutModeChanged;

        /// <summary>
        /// 引发布局模式变更事件
        /// </summary>
        protected virtual void OnLayoutModeChanged(EventArgs e)
        {
            LayoutModeChanged?.Invoke(this, e);
        }

        #endregion

        #region 公共方法


        /// <summary>
        /// 获取指定点处的分隔条索引
        /// </summary>
        public int GetSplitterIndexAtPoint(Point point)
        {
            var splitters = GetSplitterRectangles();

            Trace.WriteLine($"Point: {point.ToString()}");

            // 从后向前检查，优先识别索引较大的分隔条
            // 这样当两个分隔条接近时，会优先选中后添加的分隔条
            for (int i = splitters.Count - 1; i >= 0; i--)
            {
                if (splitters[i].Contains(point))
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// 开始分隔条拖动操作
        /// </summary>
        public void BeginSplitterDrag(int splitterIndex)
        {
            activeSplitter = splitterIndex;
            isDragging = true;
        }

        public void UpdateSplitterPosition(Point location)
        {
            if (isDragging && activeSplitter >= 0)
            {
                var splitters = GetSplitterRectangles();
                if (activeSplitter < splitters.Count)
                {
                    Rectangle splitterRect = splitters[activeSplitter];
                    bool isHorizontalSplitter = splitterRect.Width > splitterRect.Height;

                    // 计算边框偏移
                    int borderOffset = showBorder ? borderSize : 0;

                    switch (layoutMode)
                    {
                        case LayoutMode.Vertical:
                            if (isHorizontalSplitter)
                            {
                                // 垂直布局中的水平分隔条
                                if (activeSplitter == 0)
                                {
                                    // 第一个分隔条 (顶部)
                                    float newDistance = (float)(location.Y - borderOffset) / (Height - 2 * borderOffset);
                                    newDistance = Math.Max(0.1f, Math.Min(0.5f, newDistance));
                                    verticalSplitterDistance = newDistance;
                                }
                                else
                                {
                                    // 第二个分隔条 (底部)
                                    float newDistance = (float)(location.Y - borderOffset) / (Height - 2 * borderOffset);
                                    newDistance = Math.Max(0.5f, Math.Min(0.9f, newDistance));

                                    // 使用互补距离来避免第一个分隔条的移动
                                    // 第二个分隔条的位置取决于第一个分隔条的位置
                                    float firstSplitterDistance = verticalSplitterDistance;
                                    float maxSecondDistance = 1.0f - firstSplitterDistance - 0.1f;

                                    secondVerticalSplitterDistance = Math.Min(newDistance, maxSecondDistance);
                                }
                            }
                            break;

                        case LayoutMode.Horizontal:
                            if (!isHorizontalSplitter)
                            {
                                // 水平布局中的垂直分隔条
                                if (activeSplitter == 0)
                                {
                                    // 第一个分隔条 (左侧)
                                    float newDistance = (float)(location.X - borderOffset) / (Width - 2 * borderOffset);
                                    newDistance = Math.Max(0.1f, Math.Min(0.5f, newDistance));
                                    horizontalSplitterDistance = newDistance;
                                }
                                else
                                {
                                    // 第二个分隔条 (右侧)
                                    float newDistance = (float)(location.X - borderOffset) / (Width - 2 * borderOffset);
                                    newDistance = Math.Max(0.5f, Math.Min(0.9f, newDistance));

                                    // 使用互补距离
                                    float firstSplitterDistance = horizontalSplitterDistance;
                                    float maxSecondDistance = 1.0f - firstSplitterDistance - 0.1f;

                                    secondHorizontalSplitterDistance = Math.Min(newDistance, maxSecondDistance);
                                }
                            }
                            break;

                        default:
                            // 对于其他布局模式，使用原来的逻辑
                            if (isHorizontalSplitter)
                            {
                                // 水平分隔条
                                float newDistance = (float)(location.Y - borderOffset) / (Height - 2 * borderOffset);
                                newDistance = Math.Max(0.1f, Math.Min(0.9f, newDistance));
                                verticalSplitterDistance = newDistance;
                            }
                            else
                            {
                                // 垂直分隔条
                                float newDistance = (float)(location.X - borderOffset) / (Width - 2 * borderOffset);
                                newDistance = Math.Max(0.1f, Math.Min(0.9f, newDistance));
                                horizontalSplitterDistance = newDistance;
                            }
                            break;
                    }

                    PerformLayout();
                    Invalidate();
                }
            }
        }

        /// <summary>
        /// 结束分隔条拖动操作
        /// </summary>
        public void EndSplitterDrag()
        {
            isDragging = false;
            activeSplitter = -1;
        }

        /// <summary>
        /// 绘制分隔条 (供设计器使用)
        /// </summary>
        public void PaintSplitters(PaintEventArgs e)
        {
            if (DesignMode)
            {
                using (var splitterBrush = new SolidBrush(SystemColors.ControlDark))
                {
                    foreach (var rect in GetSplitterRectangles())
                    {
                        e.Graphics.FillRectangle(splitterBrush, rect);
                    }
                }
            }
        }


        /// <summary>
        /// 获取指定GridIndex的布局位置
        /// </summary>
        public LayoutPosition GetPositionByGridIndex(int gridIndex)
        {
            if (gridIndex < 1 || gridIndex > 4)
                throw new ArgumentOutOfRangeException(nameof(gridIndex), "GridIndex必须在1到4之间");

            return gridCells[gridIndex - 1].Position;
        }

        /// <summary>
        /// 手动设置指定GridIndex对应的LayoutPosition (仅在Custom模式下有效)
        /// </summary>
        public void SetPositionByGridIndex(int gridIndex, LayoutPosition position)
        {
            if (gridIndex < 1 || gridIndex > 4)
                throw new ArgumentOutOfRangeException(nameof(gridIndex), "GridIndex必须在1到4之间");

            if (layoutMode == LayoutMode.Custom)
            {
                gridCells[gridIndex - 1].Position = position;
                PerformLayout();
                Invalidate();
            }
            else
            {
                throw new InvalidOperationException("只有在Custom布局模式下才能手动设置LayoutPosition");
            }
        }

        /// <summary>
        /// 获取指定坐标位置所在的格子索引
        /// </summary>
        internal int GetGridIndexAtPoint(Point point)
        {
            // 检查每个可见的格子
            for (int i = 0; i < gridCells.Length; i++)
            {
                if (gridCells[i].Visible && gridCells[i].Bounds.Contains(point))
                {
                    return i + 1;
                }
            }

            // 如果没有找到明确的格子，返回第一个可见格子
            for (int i = 0; i < gridCells.Length; i++)
            {
                if (gridCells[i].Visible)
                {
                    return i + 1;
                }
            }

            return 1; // 默认返回第一个格子
        }

        /// <summary>
        /// 获取指定坐标位置所在的格子
        /// </summary>
        internal GridCell GetGridCellAtPoint(Point point)
        {
            int index = GetGridIndexAtPoint(point);
            return gridCells[index - 1];
        }

        #endregion

        #region 内部方法

        /// <summary>
        /// 检查指定点是否位于可拖动边缘区域
        /// </summary>
        private int GetResizableEdgeAtPoint(Point point)
        {
            // 不考虑边框的坐标调整
            int borderOffset = showBorder ? borderSize : 0;

            for (int i = 0; i < gridCells.Length; i++)
            {
                var cell = gridCells[i];
                if (!cell.Visible)
                    continue;

                Rectangle bounds = cell.Bounds;

                // 检查上边缘
                if (point.Y >= bounds.Top - RESIZE_BORDER_WIDTH &&
                    point.Y <= bounds.Top + RESIZE_BORDER_WIDTH &&
                    point.X >= bounds.Left &&
                    point.X <= bounds.Right)
                {
                    // 确保有相邻的上方格子
                    for (int j = 0; j < gridCells.Length; j++)
                    {
                        if (j != i && gridCells[j].Visible &&
                            gridCells[j].Bounds.Bottom == bounds.Top)
                            return 0; // 上边缘可调整
                    }
                }

                // 检查右边缘
                if (point.X >= bounds.Right - RESIZE_BORDER_WIDTH &&
                    point.X <= bounds.Right + RESIZE_BORDER_WIDTH &&
                    point.Y >= bounds.Top &&
                    point.Y <= bounds.Bottom)
                {
                    // 确保有相邻的右方格子
                    for (int j = 0; j < gridCells.Length; j++)
                    {
                        if (j != i && gridCells[j].Visible &&
                            gridCells[j].Bounds.Left == bounds.Right)
                            return 1; // 右边缘可调整
                    }
                }

                // 检查下边缘
                if (point.Y >= bounds.Bottom - RESIZE_BORDER_WIDTH &&
                    point.Y <= bounds.Bottom + RESIZE_BORDER_WIDTH &&
                    point.X >= bounds.Left &&
                    point.X <= bounds.Right)
                {
                    // 确保有相邻的下方格子
                    for (int j = 0; j < gridCells.Length; j++)
                    {
                        if (j != i && gridCells[j].Visible &&
                            gridCells[j].Bounds.Top == bounds.Bottom)
                            return 2; // 下边缘可调整
                    }
                }

                // 检查左边缘
                if (point.X >= bounds.Left - RESIZE_BORDER_WIDTH &&
                    point.X <= bounds.Left + RESIZE_BORDER_WIDTH &&
                    point.Y >= bounds.Top &&
                    point.Y <= bounds.Bottom)
                {
                    // 确保有相邻的左方格子
                    for (int j = 0; j < gridCells.Length; j++)
                    {
                        if (j != i && gridCells[j].Visible &&
                            gridCells[j].Bounds.Right == bounds.Left)
                            return 3; // 左边缘可调整
                    }
                }
            }

            return -1; // 不在可调整边缘
        }

        /// <summary>
        /// 根据当前LayoutMode更新每个GridIndex对应的LayoutPosition
        /// </summary>
        private void UpdateLayoutPositions()
        {
            // 根据不同布局模式，更新GridIndex到LayoutPosition的映射
            switch (layoutMode)
            {
                case LayoutMode.Vertical:
                    if (showTopPanel && showBottomPanel)
                    {
                        // 显示上/中/下三个格子
                        gridCells[0].Position = LayoutPosition.Top;
                        gridCells[1].Position = LayoutPosition.Center;
                        gridCells[2].Position = LayoutPosition.Bottom;
                    }
                    else if (showTopPanel && !showBottomPanel)
                    {
                        // 只显示上/中两个格子
                        gridCells[0].Position = LayoutPosition.Top;
                        gridCells[1].Position = LayoutPosition.Center;
                        gridCells[2].Position = LayoutPosition.None;
                    }
                    else if (!showTopPanel && showBottomPanel)
                    {
                        // 只显示中/下两个格子
                        gridCells[0].Position = LayoutPosition.None;
                        gridCells[1].Position = LayoutPosition.Center;
                        gridCells[2].Position = LayoutPosition.Bottom;
                    }
                    else
                    {
                        // 只显示中间格子
                        gridCells[0].Position = LayoutPosition.None;
                        gridCells[1].Position = LayoutPosition.Center;
                        gridCells[2].Position = LayoutPosition.None;
                    }
                    // 第四个格子始终不显示
                    gridCells[3].Position = LayoutPosition.None;
                    break;

                case LayoutMode.Horizontal:
                    if (showLeftPanel && showRightPanel)
                    {
                        // 显示左/中/右三个格子
                        gridCells[0].Position = LayoutPosition.Left;
                        gridCells[1].Position = LayoutPosition.Center;
                        gridCells[2].Position = LayoutPosition.Right;
                    }
                    else if (showLeftPanel && !showRightPanel)
                    {
                        // 只显示左/中两个格子
                        gridCells[0].Position = LayoutPosition.Left;
                        gridCells[1].Position = LayoutPosition.Center;
                        gridCells[2].Position = LayoutPosition.None;
                    }
                    else if (!showLeftPanel && showRightPanel)
                    {
                        // 只显示中/右两个格子
                        gridCells[0].Position = LayoutPosition.None;
                        gridCells[1].Position = LayoutPosition.Center;
                        gridCells[2].Position = LayoutPosition.Right;
                    }
                    else
                    {
                        // 只显示中间格子
                        gridCells[0].Position = LayoutPosition.None;
                        gridCells[1].Position = LayoutPosition.Center;
                        gridCells[2].Position = LayoutPosition.None;
                    }
                    // 第四个格子始终不显示
                    gridCells[3].Position = LayoutPosition.None;
                    break;

                case LayoutMode.Grid:
                    gridCells[0].Position = LayoutPosition.TopLeft;
                    gridCells[1].Position = LayoutPosition.TopRight;
                    gridCells[2].Position = LayoutPosition.BottomLeft;
                    gridCells[3].Position = LayoutPosition.BottomRight;
                    break;

                case LayoutMode.TopSpan:
                    gridCells[0].Position = LayoutPosition.Top;
                    gridCells[1].Position = LayoutPosition.BottomLeft;
                    gridCells[2].Position = LayoutPosition.BottomRight;
                    gridCells[3].Position = LayoutPosition.None;
                    break;

                case LayoutMode.BottomSpan:
                    gridCells[0].Position = LayoutPosition.TopLeft;
                    gridCells[1].Position = LayoutPosition.TopRight;
                    gridCells[2].Position = LayoutPosition.Bottom;
                    gridCells[3].Position = LayoutPosition.None;
                    break;

                case LayoutMode.LeftSpan:
                    gridCells[0].Position = LayoutPosition.Left;
                    gridCells[1].Position = LayoutPosition.TopRight;
                    gridCells[2].Position = LayoutPosition.BottomRight;
                    gridCells[3].Position = LayoutPosition.None;
                    break;

                case LayoutMode.RightSpan:
                    gridCells[0].Position = LayoutPosition.TopLeft;
                    gridCells[1].Position = LayoutPosition.BottomLeft;
                    gridCells[2].Position = LayoutPosition.Right;
                    gridCells[3].Position = LayoutPosition.None;
                    break;

                case LayoutMode.Custom:
                    // 自定义模式下不进行自动映射，由用户负责设置
                    break;
            }

            // 更新格子可见性
            for (int i = 0; i < gridCells.Length; i++)
            {
                gridCells[i].Visible = gridCells[i].Position != LayoutPosition.None;
            }
        }

        /// <summary>
        /// 根据布局位置计算各个区域的矩形
        /// </summary>
        private Dictionary<LayoutPosition, Rectangle> CalculateLayoutRectangles()
        {
            var result = new Dictionary<LayoutPosition, Rectangle>();
            int splitterSize = (int)this.splitterSize;

            // 计算边框占用的空间
            int borderOffset = showBorder ? borderSize : 0;

            // 调整可用区域
            int availableWidth = Width - 2 * borderOffset;
            int availableHeight = Height - 2 * borderOffset;

            // 确保可用区域有效
            availableWidth = Math.Max(0, availableWidth);
            availableHeight = Math.Max(0, availableHeight);

            // 计算主要分隔位置
            int horizontalSplit = borderOffset + (int)(availableWidth * horizontalSplitterDistance);
            int verticalSplit = borderOffset + (int)(availableHeight * verticalSplitterDistance);

            // 计算第二个分隔位置
            int secondHorizontalSplit = borderOffset + (int)(availableWidth * secondHorizontalSplitterDistance);
            int secondVerticalSplit = borderOffset + (int)(availableHeight * secondVerticalSplitterDistance);

            int horizontalSpacing = (int)Math.Ceiling(availableWidth * 0.05d);
            int verticalSpacing = (int)Math.Ceiling(availableHeight * 0.05d);

            // 确保分隔位置有效
            horizontalSplit = Math.Max(borderOffset + horizontalSpacing, Math.Min(Width - borderOffset - horizontalSpacing, horizontalSplit));
            verticalSplit = Math.Max(borderOffset + verticalSpacing, Math.Min(Height - borderOffset - verticalSpacing, verticalSplit));
            secondHorizontalSplit = Math.Max(horizontalSplit + horizontalSpacing, Math.Min(Width - borderOffset - horizontalSpacing, secondHorizontalSplit));
            secondVerticalSplit = Math.Max(verticalSplit + verticalSpacing, Math.Min(Height - borderOffset - verticalSpacing, secondVerticalSplit));


            switch (layoutMode)
            {
                case LayoutMode.Vertical:
                    {
                        bool hasTop = showTopPanel;
                        bool hasBottom = showBottomPanel;

                        // 始终有中间格子
                        bool hasCenter = true;

                        if (hasTop && hasCenter && hasBottom)
                        {
                            // 三格都有
                            result[LayoutPosition.Top] = new Rectangle(borderOffset, borderOffset, availableWidth, verticalSplit - borderOffset - splitterSize / 2);
                            result[LayoutPosition.Center] = new Rectangle(borderOffset, verticalSplit + splitterSize / 2, availableWidth, secondVerticalSplit - verticalSplit - splitterSize);
                            result[LayoutPosition.Bottom] = new Rectangle(borderOffset, secondVerticalSplit + splitterSize / 2, availableWidth, availableHeight - secondVerticalSplit - splitterSize / 2 + borderOffset);
                        }
                        else if (hasTop && hasCenter)
                        {
                            // 只有上中
                            result[LayoutPosition.Top] = new Rectangle(borderOffset, borderOffset, availableWidth, verticalSplit - borderOffset - splitterSize / 2);
                            result[LayoutPosition.Center] = new Rectangle(borderOffset, verticalSplit + splitterSize / 2, availableWidth, availableHeight - verticalSplit - splitterSize / 2 + borderOffset);
                        }
                        else if (hasCenter && hasBottom)
                        {
                            // 只有中下
                            result[LayoutPosition.Center] = new Rectangle(borderOffset, borderOffset, availableWidth, secondVerticalSplit - splitterSize / 2);
                            result[LayoutPosition.Bottom] = new Rectangle(borderOffset, secondVerticalSplit + splitterSize / 2, availableWidth, availableHeight - secondVerticalSplit - splitterSize / 2 + borderOffset);
                        }
                        else if (hasTop && hasBottom)
                        {
                            // 只有上下
                            result[LayoutPosition.Top] = new Rectangle(borderOffset, borderOffset, availableWidth, verticalSplit - borderOffset - splitterSize / 2);
                            result[LayoutPosition.Bottom] = new Rectangle(borderOffset, verticalSplit + splitterSize / 2, availableWidth, availableHeight - verticalSplit - splitterSize / 2 + borderOffset);
                        }
                        else if (hasTop)
                        {
                            // 只有上
                            result[LayoutPosition.Top] = new Rectangle(borderOffset, borderOffset, availableWidth, availableHeight);
                        }
                        else if (hasCenter)
                        {
                            // 只有中
                            result[LayoutPosition.Center] = new Rectangle(borderOffset, borderOffset, availableWidth, availableHeight);
                        }
                        else if (hasBottom)
                        {
                            // 只有下
                            result[LayoutPosition.Bottom] = new Rectangle(borderOffset, borderOffset, availableWidth, availableHeight);
                        }
                    }
                    break;

                case LayoutMode.Horizontal:
                    // 水平布局实现 - 类似垂直布局但方向相反
                    {
                        bool hasLeft = showLeftPanel;
                        bool hasRight = showRightPanel;

                        // 始终有中间格子
                        bool hasCenter = true;

                        if (hasLeft && hasCenter && hasRight)
                        {
                            // 三格都有
                            result[LayoutPosition.Left] = new Rectangle(borderOffset, borderOffset, horizontalSplit - borderOffset - splitterSize / 2, availableHeight);
                            result[LayoutPosition.Center] = new Rectangle(horizontalSplit + splitterSize / 2, borderOffset, secondHorizontalSplit - horizontalSplit - splitterSize, availableHeight);
                            result[LayoutPosition.Right] = new Rectangle(secondHorizontalSplit + splitterSize / 2, borderOffset, availableWidth - secondHorizontalSplit - splitterSize / 2 + borderOffset, availableHeight);
                        }
                        else if (hasLeft && hasCenter)
                        {
                            // 只有左中
                            result[LayoutPosition.Left] = new Rectangle(borderOffset, borderOffset, horizontalSplit - borderOffset - splitterSize / 2, availableHeight);
                            result[LayoutPosition.Center] = new Rectangle(horizontalSplit + splitterSize / 2, borderOffset, availableWidth - horizontalSplit - splitterSize / 2 + borderOffset, availableHeight);
                        }
                        else if (hasCenter && hasRight)
                        {
                            // 只有中右
                            result[LayoutPosition.Center] = new Rectangle(borderOffset, borderOffset, secondHorizontalSplit - splitterSize / 2, availableHeight);
                            result[LayoutPosition.Right] = new Rectangle(secondHorizontalSplit + splitterSize / 2, borderOffset, availableWidth - secondHorizontalSplit - splitterSize / 2 + borderOffset, availableHeight);
                        }
                        else if (hasLeft && hasRight)
                        {
                            // 只有左右
                            result[LayoutPosition.Left] = new Rectangle(borderOffset, borderOffset, horizontalSplit - borderOffset - splitterSize / 2, availableHeight);
                            result[LayoutPosition.Right] = new Rectangle(horizontalSplit + splitterSize / 2, borderOffset, availableWidth - horizontalSplit - splitterSize / 2 + borderOffset, availableHeight);
                        }
                        else if (hasLeft)
                        {
                            // 只有左
                            result[LayoutPosition.Left] = new Rectangle(borderOffset, borderOffset, availableWidth, availableHeight);
                        }
                        else if (hasCenter)
                        {
                            // 只有中
                            result[LayoutPosition.Center] = new Rectangle(borderOffset, borderOffset, availableWidth, availableHeight);
                        }
                        else if (hasRight)
                        {
                            // 只有右
                            result[LayoutPosition.Right] = new Rectangle(borderOffset, borderOffset, availableWidth, availableHeight);
                        }
                    }
                    break;
                case LayoutMode.Grid:
                    // 网格布局实现
                    result[LayoutPosition.TopLeft] = new Rectangle(borderOffset, borderOffset, horizontalSplit - borderOffset - splitterSize / 2, verticalSplit - borderOffset - splitterSize / 2);
                    result[LayoutPosition.TopRight] = new Rectangle(horizontalSplit + splitterSize / 2, borderOffset, availableWidth - horizontalSplit - splitterSize / 2 + borderOffset, verticalSplit - borderOffset - splitterSize / 2);
                    result[LayoutPosition.BottomLeft] = new Rectangle(borderOffset, verticalSplit + splitterSize / 2, horizontalSplit - borderOffset - splitterSize / 2, availableHeight - verticalSplit - splitterSize / 2 + borderOffset);
                    result[LayoutPosition.BottomRight] = new Rectangle(horizontalSplit + splitterSize / 2, verticalSplit + splitterSize / 2, availableWidth - horizontalSplit - splitterSize / 2 + borderOffset, availableHeight - verticalSplit - splitterSize / 2 + borderOffset);
                    break;

                case LayoutMode.TopSpan:
                    // 上部区域一格占满，下部分为左右两个区域
                    result[LayoutPosition.Top] = new Rectangle(borderOffset, borderOffset, availableWidth, verticalSplit - borderOffset - splitterSize / 2);
                    result[LayoutPosition.BottomLeft] = new Rectangle(borderOffset, verticalSplit + splitterSize / 2, horizontalSplit - borderOffset - splitterSize / 2, availableHeight - verticalSplit - splitterSize / 2 + borderOffset);
                    result[LayoutPosition.BottomRight] = new Rectangle(horizontalSplit + splitterSize / 2, verticalSplit + splitterSize / 2, availableWidth - horizontalSplit - splitterSize / 2 + borderOffset, availableHeight - verticalSplit - splitterSize / 2 + borderOffset);
                    break;

                case LayoutMode.BottomSpan:
                    // 下部区域一格占满，上部分为左右两个区域
                    result[LayoutPosition.TopLeft] = new Rectangle(borderOffset, borderOffset, horizontalSplit - borderOffset - splitterSize / 2, verticalSplit - borderOffset - splitterSize / 2);
                    result[LayoutPosition.TopRight] = new Rectangle(horizontalSplit + splitterSize / 2, borderOffset, availableWidth - horizontalSplit - splitterSize / 2 + borderOffset, verticalSplit - borderOffset - splitterSize / 2);
                    result[LayoutPosition.Bottom] = new Rectangle(borderOffset, verticalSplit + splitterSize / 2, availableWidth, availableHeight - verticalSplit - splitterSize / 2 + borderOffset);
                    break;

                case LayoutMode.LeftSpan:
                    // 左部区域一格占满，右部分为上下两个区域
                    result[LayoutPosition.Left] = new Rectangle(borderOffset, borderOffset, horizontalSplit - borderOffset - splitterSize / 2, availableHeight);
                    result[LayoutPosition.TopRight] = new Rectangle(horizontalSplit + splitterSize / 2, borderOffset, availableWidth - horizontalSplit - splitterSize / 2 + borderOffset, verticalSplit - borderOffset - splitterSize / 2);
                    result[LayoutPosition.BottomRight] = new Rectangle(horizontalSplit + splitterSize / 2, verticalSplit + splitterSize / 2, availableWidth - horizontalSplit - splitterSize / 2 + borderOffset, availableHeight - verticalSplit - splitterSize / 2 + borderOffset);
                    break;

                case LayoutMode.RightSpan:
                    // 右部区域一格占满，左部分为上下两个区域
                    result[LayoutPosition.TopLeft] = new Rectangle(borderOffset, borderOffset, horizontalSplit - borderOffset - splitterSize / 2, verticalSplit - borderOffset - splitterSize / 2);
                    result[LayoutPosition.BottomLeft] = new Rectangle(borderOffset, verticalSplit + splitterSize / 2, horizontalSplit - borderOffset - splitterSize / 2, availableHeight - verticalSplit - splitterSize / 2 + borderOffset);
                    result[LayoutPosition.Right] = new Rectangle(horizontalSplit + splitterSize / 2, borderOffset, availableWidth - horizontalSplit - splitterSize / 2 + borderOffset, availableHeight);
                    break;

                case LayoutMode.Custom:
                    // 自定义模式使用Grid布局的计算方式作为基础
                    {
                        result[LayoutPosition.TopLeft] = new Rectangle(borderOffset, borderOffset, horizontalSplit - borderOffset - splitterSize / 2, verticalSplit - borderOffset - splitterSize / 2);
                        result[LayoutPosition.TopRight] = new Rectangle(horizontalSplit + splitterSize / 2, borderOffset, availableWidth - horizontalSplit - splitterSize / 2 + borderOffset, verticalSplit - borderOffset - splitterSize / 2);
                        result[LayoutPosition.BottomLeft] = new Rectangle(borderOffset, verticalSplit + splitterSize / 2, horizontalSplit - borderOffset - splitterSize / 2, availableHeight - verticalSplit - splitterSize / 2 + borderOffset);
                        result[LayoutPosition.BottomRight] = new Rectangle(horizontalSplit + splitterSize / 2, verticalSplit + splitterSize / 2, availableWidth - horizontalSplit - splitterSize / 2 + borderOffset, availableHeight - verticalSplit - splitterSize / 2 + borderOffset);

                        // 其他位置可以根据需要添加
                        result[LayoutPosition.Top] = new Rectangle(borderOffset, borderOffset, availableWidth, verticalSplit - borderOffset - splitterSize / 2);
                        result[LayoutPosition.Bottom] = new Rectangle(borderOffset, verticalSplit + splitterSize / 2, availableWidth, availableHeight - verticalSplit - splitterSize / 2 + borderOffset);
                        result[LayoutPosition.Left] = new Rectangle(borderOffset, borderOffset, horizontalSplit - borderOffset - splitterSize / 2, availableHeight);
                        result[LayoutPosition.Right] = new Rectangle(horizontalSplit + splitterSize / 2, borderOffset, availableWidth - horizontalSplit - splitterSize / 2 + borderOffset, availableHeight);
                        result[LayoutPosition.Center] = new Rectangle(borderOffset, borderOffset, availableWidth, availableHeight);
                    }
                    break;
            }

            return result;
        }

        /// <summary>
        /// 检查是否有指定布局位置的可见格子
        /// </summary>
        private bool HasVisiblePosition(LayoutPosition position)
        {
            return gridCells.Any(c => c.Position == position);
        }

        /// <summary>
        /// 获取分隔条的矩形区域
        /// </summary>
        private List<Rectangle> GetSplitterRectangles()
        {
            var result = new List<Rectangle>();
            int splitterSize = (int)this.splitterSize;

            // 计算边框占用的空间
            int borderOffset = showBorder ? borderSize : 0;

            // 调整可用区域
            int availableWidth = Width - 2 * borderOffset;
            int availableHeight = Height - 2 * borderOffset;

            // 确保可用区域有效
            availableWidth = Math.Max(0, availableWidth);
            availableHeight = Math.Max(0, availableHeight);

            // 计算主要分隔位置
            int horizontalSplit = borderOffset + (int)(availableWidth * horizontalSplitterDistance);
            int verticalSplit = borderOffset + (int)(availableHeight * verticalSplitterDistance);

            // 计算第二个分隔位置
            int secondHorizontalSplit = borderOffset + (int)(availableWidth * secondHorizontalSplitterDistance);
            int secondVerticalSplit = borderOffset + (int)(availableHeight * secondVerticalSplitterDistance);

            // 确保分隔位置有效
            horizontalSplit = Math.Max(borderOffset + 10, Math.Min(Width - borderOffset - 10, horizontalSplit));
            verticalSplit = Math.Max(borderOffset + 10, Math.Min(Height - borderOffset - 10, verticalSplit));
            secondHorizontalSplit = Math.Max(horizontalSplit + 20, Math.Min(Width - borderOffset - 10, secondHorizontalSplit));
            secondVerticalSplit = Math.Max(verticalSplit + 20, Math.Min(Height - borderOffset - 10, secondVerticalSplit));

            switch (layoutMode)
            {
                case LayoutMode.Vertical:
                    {
                        bool hasTop = showTopPanel;
                        bool hasBottom = showBottomPanel;

                        // 始终有中间格子
                        bool hasCenter = true;

                        if (hasTop && hasCenter)
                        {
                            // Top 和 Center 之间的分隔条
                            //result.Add(new Rectangle(0, verticalSplit - splitterSize / 2, Width, splitterSize));
                            result.Add(new Rectangle(borderOffset, verticalSplit - splitterSize / 2, availableWidth, splitterSize));
                        }

                        if (hasCenter && hasBottom)
                        {
                            // Center 和 Bottom 之间的分隔条
                            //result.Add(new Rectangle(0, secondVerticalSplit - splitterSize / 2, Width, splitterSize));
                            result.Add(new Rectangle(borderOffset, secondVerticalSplit - splitterSize / 2, availableWidth, splitterSize));
                        }
                    }
                    break;

                case LayoutMode.Horizontal:
                    {
                        bool hasLeft = showLeftPanel;
                        bool hasRight = showRightPanel;

                        // 始终有中间格子
                        bool hasCenter = true;

                        if (hasLeft && hasCenter)
                        {
                            // Left 和 Center 之间的分隔条
                            result.Add(new Rectangle(horizontalSplit - splitterSize / 2, 0, splitterSize, Height));
                        }

                        if (hasCenter && hasRight)
                        {
                            // Center 和 Right 之间的分隔条
                            result.Add(new Rectangle(secondHorizontalSplit - splitterSize / 2, 0, splitterSize, Height));
                        }
                    }
                    break;

                case LayoutMode.Grid:
                case LayoutMode.TopSpan:
                case LayoutMode.BottomSpan:
                case LayoutMode.LeftSpan:
                case LayoutMode.RightSpan:
                case LayoutMode.Custom:
                    // 垂直分隔线
                    result.Add(new Rectangle(horizontalSplit - splitterSize / 2, 0, splitterSize, Height));
                    // 水平分隔线
                    result.Add(new Rectangle(0, verticalSplit - splitterSize / 2, Width, splitterSize));
                    break;
            }

            return result;
        }        /// <summary>
                 /// 获取鼠标指针下的分隔条索引
                 /// </summary>
        private int GetSplitterAtPoint(Point point)
        {
            var splitters = GetSplitterRectangles();
            for (int i = 0; i < splitters.Count; i++)
            {
                if (splitters[i].Contains(point))
                {
                    return i;
                }
            }
            return -1;
        }

        #endregion

        #region 重写方法

        /// <summary>
        /// 处理分隔条事件以及绘制
        /// </summary>
        //protected override void WndProc(ref Message m)
        //{
        //    base.WndProc(ref m);

        //    // WM_PAINT消息
        //    if (m.Msg == 0x000F)
        //    {
        //        // 在处理完所有其他绘制后，单独绘制分隔条
        //        using (Graphics g = Graphics.FromHwnd(this.Handle))
        //        {
        //            // 绘制分隔条
        //            using (var splitterBrush = new SolidBrush(SystemColors.ControlDark))
        //            {
        //                foreach (var rect in GetSplitterRectangles())
        //                {
        //                    g.FillRectangle(splitterBrush, rect);

        //                    // 如果正在拖动，绘制高亮效果
        //                    if (_isDragging)
        //                    {
        //                        using (var highlightPen = new Pen(Color.White, 1))
        //                        {
        //                            g.DrawRectangle(highlightPen, rect);
        //                        }
        //                    }
        //                }
        //            }
        //        }
        //    }
        //}


        /// <summary>
        /// 处理布局逻辑
        /// </summary>
        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);

            if (Width <= 0 || Height <= 0)
                return;

            var layoutRects = CalculateLayoutRectangles();

            // 设置每个格子的位置和大小
            foreach (var cell in gridCells)
            {
                if (cell.Position != LayoutPosition.None && layoutRects.ContainsKey(cell.Position))
                {
                    Rectangle rect = layoutRects[cell.Position];
                    cell.Bounds = rect;
                    cell.Visible = true;
                }
                else
                {
                    cell.Visible = false;
                }
            }
        }

        /// <summary>
        /// 绘制控件
        /// </summary>
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            // 绘制背景
            e.Graphics.Clear(BackColor);

            // 绘制分隔条
            using (var splitterBrush = new SolidBrush(SystemColors.ControlDark))
            {
                foreach (var rect in GetSplitterRectangles())
                {
                    e.Graphics.FillRectangle(splitterBrush, rect);
                }
            }

            // 绘制边框
            if (showBorder && borderSize > 0)
            {
                using (var pen = new Pen(borderColor, borderSize))
                {
                    // 绘制四边
                    int halfPenWidth = borderSize / 2;
                    e.Graphics.DrawRectangle(pen,
                        halfPenWidth,
                        halfPenWidth,
                        Width - borderSize,
                        Height - borderSize);
                }
            }

            // 在设计模式下，绘制格子边缘提示线
            if (DesignMode)
            {
                using (var pen = new Pen(Color.Gray, 1) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dot })
                {
                    foreach (var cell in gridCells)
                    {
                        if (cell.Visible)
                        {
                            e.Graphics.DrawRectangle(pen, cell.Bounds);
                        }
                    }
                }
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            base.OnPaintBackground(e);
        }

        protected override void OnControlAdded(ControlEventArgs e)
        {
            base.OnControlAdded(e);
            Invalidate(); // 确保控件重绘
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (e.Button == MouseButtons.Left)
            {
                activeSplitter = GetSplitterAtPoint(e.Location);
                if (activeSplitter >= 0)
                {
                    isDragging = true;
                    dragStartPoint = e.Location;
                    Capture = true;
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            // 更新光标
            int splitterIndex = GetSplitterAtPoint(e.Location);
            if (splitterIndex >= 0)
            {
                // 设置适当的光标形状
                var splitterRect = GetSplitterRectangles()[splitterIndex];
                Cursor = splitterRect.Width > splitterRect.Height ? Cursors.HSplit : Cursors.VSplit;
            }
            else
            {
                Cursor = Cursors.Default;
            }

            // 处理拖动
            if (isDragging && e.Button == MouseButtons.Left && activeSplitter >= 0)
            {
                var splitters = GetSplitterRectangles();
                if (activeSplitter < splitters.Count)
                {
                    Rectangle splitterRect = splitters[activeSplitter];

                    // 调整水平分隔条
                    if (splitterRect.Width > splitterRect.Height)
                    {
                        float newDistance = (float)e.Y / Height;
                        newDistance = Math.Max(0.1f, Math.Min(0.9f, newDistance));

                        if (activeSplitter == 0)
                        {
                            VerticalSplitterDistance = newDistance;
                        }
                        else
                        {
                            SecondVerticalSplitterDistance = newDistance;
                        }
                    }
                    // 调整垂直分隔条
                    else
                    {
                        float newDistance = (float)e.X / Width;
                        newDistance = Math.Max(0.1f, Math.Min(0.9f, newDistance));
                        if (activeSplitter == 0)
                        {
                            HorizontalSplitterDistance = newDistance;
                        }
                        else
                        {
                            SecondHorizontalSplitterDistance = newDistance;
                        }
                    }

                    PerformLayout();
                    Invalidate();
                }
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);

            if (isDragging)
            {
                isDragging = false;
                activeSplitter = -1;
                Capture = false;
                Cursor = Cursors.Default;
            }
        }

        #endregion

    }

    /// <summary>
    /// 布局模式枚举 - 支持多种灵活布局方式
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
    /// 布局位置枚举 - 支持1-4格灵活布局
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



    /// <summary>
    /// 格子容器控件 - 表示LayoutContainer中的一个区域
    /// </summary>
    [Designer("System.Windows.Forms.Design.ScrollableControlDesigner, System.Design")]
    [ToolboxItem(false)]
    public class GridCell : Panel
    {
        /// <summary>
        /// 获取网格索引（1-4）
        /// </summary>
        [Browsable(false)]
        public int GridIndex { get; }

        /// <summary>
        /// 获取或设置布局位置
        /// </summary>
        [Browsable(false)]
        public LayoutPosition Position { get; set; }

        /// <summary>
        /// 初始化格子容器的新实例
        /// </summary>
        public GridCell(int gridIndex)
        {
            GridIndex = gridIndex;
            Position = LayoutPosition.None;

            // 设置默认属性
            BorderStyle = BorderStyle.None;
            Dock = DockStyle.None;
            Padding = new Padding(0);
            Margin = new Padding(0);
        }

        protected override void OnControlAdded(ControlEventArgs e)
        {
            base.OnControlAdded(e);

            // 设置默认停靠模式
            if (e.Control.Dock == DockStyle.None)
            {
                e.Control.Dock = DockStyle.Fill;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            // 在设计模式下显示位置提示
            if (DesignMode && Controls.Count == 0)
            {
                // 绘制提示文本
                using (Font font = new Font(FontFamily.GenericSansSerif, 8))
                using (StringFormat format = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                })
                {
                    string text = $"GridCell {GridIndex}\n({Position})";
                    e.Graphics.DrawString(text, font, SystemBrushes.GrayText, new RectangleF(0, 0, Width, Height), format);
                }
            }
        }

    }


    /// <summary>
    /// LayoutContainer设计器类 - 提供设计时支持
    /// </summary>
    public class LayoutContainerDesigner : ParentControlDesigner
    {
        private LayoutContainer Control => (LayoutContainer)Component;
        private ISelectionService _selectionService;
        private IComponentChangeService _changeService;
        private bool _draggingSplitter = false;
        private int _activeSplitterIndex = -1;

        /// <summary>
        /// 初始化设计器
        /// </summary>
        public override void Initialize(IComponent component)
        {
            base.Initialize(component);

            // 获取设计器服务
            _selectionService = (ISelectionService)GetService(typeof(ISelectionService));
            _changeService = (IComponentChangeService)GetService(typeof(IComponentChangeService));

            // 启用格子的设计模式
            EnableDesignMode(Control.Panel1, "Panel1");
            EnableDesignMode(Control.Panel2, "Panel2");
            EnableDesignMode(Control.Panel3, "Panel3");
            EnableDesignMode(Control.Panel4, "Panel4");
        }

        /// <summary>
        /// 设置设计器中子控件的行为
        /// </summary>
        protected override void PreFilterProperties(IDictionary properties)
        {
            base.PreFilterProperties(properties);

            // 隐藏不需要在属性窗口中显示的属性
            properties.Remove("Controls");
            properties.Remove("DefaultSize");
            properties.Remove("Padding");
        }

        /// <summary>
        /// 重写拖放行为 - 允许在分隔条上操作，但不允许直接拖放到LayoutContainer上
        /// </summary>
        protected override bool GetHitTest(Point point)
        {
            // 将坐标从设计器视图转换到控件坐标
            Point controlPoint = Control.PointToClient(point);

            // 检查点是否在分隔条上
            int splitterIndex = Control.GetSplitterIndexAtPoint(controlPoint);
            if (splitterIndex >= 0)
            {
                // 如果在分隔条上，记录当前正在拖动的分隔条
                if (!_draggingSplitter)
                {
                    _draggingSplitter = true;
                    _activeSplitterIndex = splitterIndex;
                    Control.BeginSplitterDrag(splitterIndex);
                }
                return true;
            }
            else if (_draggingSplitter)
            {
                // 鼠标松开,已完成拖动
                if (LayoutContainer.MouseButtons == MouseButtons.None)
                {
                    _draggingSplitter = false;
                    _activeSplitterIndex = -1;
                    Control.EndSplitterDrag();
                }
                //按下拖动
                else
                {
                    // 如果已经在拖动，更新分隔条位置
                    Control.UpdateSplitterPosition(controlPoint);
                }
            }

            // 不允许直接在LayoutContainer上拖放
            return false;
        }

        /// <summary>
        /// 确定控件是否可以被这个设计器处理
        /// </summary>
        public override bool CanParent(Control control)
        {
            // 只允许GridCell作为LayoutContainer的直接子控件
            return control is GridCell;
        }

        /// <summary>
        /// 绘制设计时的辅助元素
        /// </summary>
        protected override void OnPaintAdornments(PaintEventArgs pe)
        {
            base.OnPaintAdornments(pe);

            // 在设计时绘制分隔条
            Control.PaintSplitters(pe);
        }

        public override DesignerActionListCollection ActionLists
        {
            get
            {
                DesignerActionListCollection actionLists = new DesignerActionListCollection();
                actionLists.Add(new LayoutContainerActionList(Control));
                return actionLists;
            }
        }

        /// <summary>
        /// 清理设计器资源
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            _selectionService = null;
            _changeService = null;

            base.Dispose(disposing);
        }
    }


    public class LayoutContainerActionList : DesignerActionList
    {
        private LayoutContainer layoutContainer;
        private readonly IDesignerHost _host;
        private bool isDocked = false;

        public LayoutContainerActionList(IComponent component) : base(component)
        {
            layoutContainer = component as LayoutContainer;
            _host = GetService(typeof(IDesignerHost)) as IDesignerHost;
            isDocked = layoutContainer.Dock == DockStyle.Fill;
        }

        //private string GetActionName()
        //{
        //    if (Component is null)
        //    {
        //        return null;
        //    }

        //    PropertyDescriptor dockProp = GetPropertyByName("Dock");
        //    if (dockProp != null)
        //    {
        //        DockStyle dockStyle = (DockStyle)dockProp.GetValue(layoutContainer);
        //        if (dockStyle == DockStyle.Fill)
        //        {
        //            return "取消在父容器中停靠";
        //        }
        //        else
        //        {
        //            return "在父容器中停靠";
        //        }
        //    }

        //    return null;
        //}


        public LayoutMode LayoutMode
        {
            get => layoutContainer.LayoutModeValue;
            set
            {
                PropertyDescriptor property = TypeDescriptor.GetProperties(layoutContainer)["LayoutModeValue"];
                GetPropertyByName("LayoutModeValue").SetValue(layoutContainer, value);

            }
        }



        public void ToggleDockInParent()
        {
            if (layoutContainer.Parent == null)
            {
                return;
            }
            if (!isDocked)
            {
                layoutContainer.Dock = DockStyle.Fill;
                isDocked = true;
            }
            else
            {
                layoutContainer.Dock = DockStyle.None;
                isDocked = false;
            }

        }

        //public override DesignerActionItemCollection GetSortedActionItems()
        //{
        //    DesignerActionItemCollection items = new DesignerActionItemCollection();
        //    string? actionName = GetActionName();
        //    if (actionName is not null)
        //    {
        //        items.Add(new DesignerActionVerbItem(new DesignerVerb(actionName, OnDockActionClick)));
        //    }

        //    return items;
        //}


        public override DesignerActionItemCollection GetSortedActionItems()
        {
            var items = new DesignerActionItemCollection
            {
                new DesignerActionHeaderItem("布局设置"),
                new DesignerActionPropertyItem("LayoutMode", "布局模式", "布局设置", "选择控件的布局模式"),
                new DesignerActionMethodItem(this, "ToggleDockInParent", isDocked ? "取消在父容器中停靠" : "在父容器中停靠", "布局设置", "设置停靠选项", true)

                //items.Add(new DesignerActionVerbItem(new DesignerVerb(actionName, OnDockActionClick)));
            };

            return items;
        }


        //private void OnDockActionClick(object sender, EventArgs e)
        //{
        //    if (sender is DesignerVerb designerVerb && _host!= null)
        //    {
        //        using (DesignerTransaction t = _host.CreateTransaction(designerVerb.Text))
        //        {
        //            PropertyDescriptor dockProp = GetPropertyByName("Dock");
        //            DockStyle dockStyle = (DockStyle)dockProp.GetValue(layoutContainer);
        //            dockProp.SetValue(layoutContainer, dockStyle == DockStyle.Fill ? DockStyle.None : DockStyle.Fill);
        //            t.Commit();
        //        }
        //    }
        //}


        private PropertyDescriptor GetPropertyByName(string propName)
        {
            PropertyDescriptor prop = TypeDescriptor.GetProperties(layoutContainer)[propName];
            if (prop == null)
            {
                throw new ArgumentException("未找到属性", propName);
            }
            return prop;
        }
    }

}

