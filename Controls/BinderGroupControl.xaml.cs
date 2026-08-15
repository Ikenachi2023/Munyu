using System;
using System.Collections.Generic;
using System.IO;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

// Explicit aliases for WPF vs WinForms
using UserControl = System.Windows.Controls.UserControl;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;

using Munyu.Models;

namespace Munyu.Controls
{
    public partial class BinderGroupControl : UserControl
    {
        private readonly List<ItemCard> _childCards = new();

        public static readonly DependencyProperty BinderItemProperty =
            DependencyProperty.Register(nameof(BinderItem), typeof(MunyuItem), typeof(BinderGroupControl), new PropertyMetadata(null, OnBinderItemChanged));

        public MunyuItem? BinderItem
        {
            get => (MunyuItem?)GetValue(BinderItemProperty);
            set => SetValue(BinderItemProperty, value);
        }

        public event EventHandler<MunyuItem>? CollapseRequested;
        public event EventHandler<MunyuItem>? ChildItemSelected;
        public event EventHandler<MunyuItem>? ChildItemDeleteRequested;
        public event EventHandler? DragOutCompleted;

        public BinderGroupControl()
        {
            InitializeComponent();
        }

        private static void OnBinderItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is BinderGroupControl group && e.NewValue is MunyuItem binder)
            {
                group.DataContext = binder;
                group.RenderChildren();
            }
        }

        public void RenderChildren()
        {
            ChildrenWrapPanel.Children.Clear();
            _childCards.Clear();

            if (BinderItem?.Children == null) return;

            foreach (var child in BinderItem.Children)
            {
                var card = new ItemCard
                {
                    Item = child
                };

                card.ItemSelected += (s, item) => ChildItemSelected?.Invoke(this, item);
                card.ItemDeleteRequested += (s, item) => OnChildDeleteRequested(item);
                card.DragOutCompleted += (s, ev) => DragOutCompleted?.Invoke(this, ev);

                _childCards.Add(card);
                ChildrenWrapPanel.Children.Add(card);
            }
        }

        public void UpdateBoundsConstraint(double islandWidth, double islandHeight, bool isVerticalDock)
        {
            int childCount = BinderItem?.Children?.Count ?? 0;
            if (childCount == 0) return;

            if (isVerticalDock)
            {
                double usableW = Math.Max(80, islandWidth - 28);
                OuterBorder.MaxWidth = usableW;
                OuterBorder.ClearValue(Border.MaxHeightProperty);
                ChildrenWrapPanel.Orientation = System.Windows.Controls.Orientation.Horizontal;

                double cardW = 76;
                double cardH = 86;

                int cols = (int)Math.Max(1, Math.Floor(usableW / 82.0));
                if (cols > childCount) cols = childCount;

                if (cols > 0)
                {
                    double calcW = (usableW - (cols * 8)) / cols;
                    if (calcW < 76)
                    {
                        cardW = Math.Max(52, calcW);
                        cardH = Math.Round(cardW * (86.0 / 76.0));
                    }
                }

                foreach (var childCard in _childCards)
                {
                    childCard.SetCardScale(cardW, cardH);
                }
            }
            else
            {
                double usableH = Math.Max(80, islandHeight - 28);
                OuterBorder.MaxHeight = usableH;
                OuterBorder.ClearValue(Border.MaxWidthProperty);
                ChildrenWrapPanel.Orientation = System.Windows.Controls.Orientation.Vertical;

                double cardW = 76;
                double cardH = 86;

                int rows = (int)Math.Max(1, Math.Floor(usableH / 92.0));
                if (rows > childCount) rows = childCount;

                if (rows > 0)
                {
                    double calcH = (usableH - (rows * 8)) / rows;
                    if (calcH < 86)
                    {
                        cardH = Math.Max(60, calcH);
                        cardW = Math.Round(cardH * (76.0 / 86.0));
                    }
                }

                foreach (var childCard in _childCards)
                {
                    childCard.SetCardScale(cardW, cardH);
                }
            }
        }

        private void OnChildDeleteRequested(MunyuItem childItem)
        {
            if (BinderItem?.Children != null)
            {
                BinderItem.Children.Remove(childItem);
                RenderChildren();
                ChildItemDeleteRequested?.Invoke(this, childItem);
            }
        }

        private void HeaderNotch_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2 && BinderItem != null)
            {
                CollapseRequested?.Invoke(this, BinderItem);
                e.Handled = true;
            }
        }
    }
}
