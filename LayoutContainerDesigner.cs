using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms.Design;
using System.Windows.Forms;

namespace LayoutDemo
{

    public class LayoutContainerDesigner : ParentControlDesigner
    {
        private LayoutContainer Control => (LayoutContainer)Component;
        private ISelectionService selectionService;
        private IComponentChangeService changeService;
        private bool draggingSplitter = false;
        private int activeSplitterIndex = -1;

        /// <summary>
        /// 初始化设计器
        /// </summary>
        public override void Initialize(IComponent component)
        {
            base.Initialize(component);

            // 获取设计器服务
            selectionService = (ISelectionService)GetService(typeof(ISelectionService));
            changeService = (IComponentChangeService)GetService(typeof(IComponentChangeService));

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
        /// 重写拖放行为
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
                if (!draggingSplitter)
                {
                    draggingSplitter = true;
                    activeSplitterIndex = splitterIndex;
                    Control.BeginSplitterDrag(splitterIndex);
                }
                return true;
            }
            else if (draggingSplitter)
            {
                // 鼠标松开,已完成拖动
                if (LayoutContainer.MouseButtons == MouseButtons.None)
                {
                    draggingSplitter = false;
                    activeSplitterIndex = -1;
                    Control.EndSplitterDrag();
                }
                //按下拖动
                else
                {
                    // 如果已经在拖动，更新分隔条位置
                    Control.UpdateSplitterPosition(controlPoint);
                }
            }

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

        protected override void Dispose(bool disposing)
        {
            selectionService = null;
            changeService = null;

            base.Dispose(disposing);
        }
    }

    public class LayoutContainerActionList : DesignerActionList
    {
        private LayoutContainer layoutContainer;
        private readonly IDesignerHost host;
        private bool isDocked = false;

        public LayoutContainerActionList(IComponent component) : base(component)
        {
            layoutContainer = component as LayoutContainer;
            host = GetService(typeof(IDesignerHost)) as IDesignerHost;
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
