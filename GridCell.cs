using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing.Printing;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LayoutDemo
{
    /// <summary>
    /// 格子容器控件 - 表示LayoutContainer中的一个区域
    /// </summary>
    [Designer("System.Windows.Forms.Design.ScrollableControlDesigner, System.Design")]
    [ToolboxItem(false)]
    public class GridCell : Panel
    {
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

}
