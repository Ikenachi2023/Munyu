using System;
using System.Collections.Generic;
using System.IO;
using System.Media;
using System.Windows;
using System.Windows.Media;

// Explicit aliases to resolve WinForms vs WPF conflicts
using Color = System.Windows.Media.Color;
using Brushes = System.Windows.Media.Brushes;
using UserControl = System.Windows.Controls.UserControl;
using Point = System.Windows.Point;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using MouseButtonState = System.Windows.Input.MouseButtonState;
using DataObject = System.Windows.DataObject;
using DataFormats = System.Windows.DataFormats;
using DragDrop = System.Windows.DragDrop;
using DragDropEffects = System.Windows.DragDropEffects;
using ContextMenuStrip = System.Windows.Forms.ContextMenuStrip;
using ToolStripSeparator = System.Windows.Forms.ToolStripSeparator;

using Munyu.Models;

namespace Munyu.Controls
{
    public partial class ItemCard : UserControl
    {
        private Point _dragStartPoint;
        private bool _isMouseDown;

        public static readonly DependencyProperty ItemProperty =
            DependencyProperty.Register(nameof(Item), typeof(MunyuItem), typeof(ItemCard), new PropertyMetadata(null, OnItemChanged));

        public MunyuItem? Item
        {
            get => (MunyuItem?)GetValue(ItemProperty);
            set => SetValue(ItemProperty, value);
        }

        public event EventHandler<MunyuItem>? ItemSelected;
        public event EventHandler<MunyuItem>? ItemDeleteRequested;
        public event EventHandler<MunyuItem>? BinderUnpackRequested;
        public event EventHandler<MunyuItem>? BinderExpandRequested;
        public event EventHandler? DragOutCompleted;

        public ItemCard()
        {
            InitializeComponent();

            MouseLeftButtonDown += ItemCard_MouseLeftButtonDown;
            MouseMove += ItemCard_MouseMove;
            MouseLeftButtonUp += ItemCard_MouseLeftButtonUp;
            MouseRightButtonUp += ItemCard_MouseRightButtonUp;
        }

        public void SetCardScale(double cardWidth, double cardHeight)
        {
            Width = Math.Max(48, cardWidth);
            Height = Math.Max(56, cardHeight);

            // Scale icon image
            double iconSize = Math.Max(20, Math.Min(32, cardWidth * 0.42));
            ItemIconImage.Width = iconSize;
            ItemIconImage.Height = iconSize;

            // Scale badge if binder
            if (BinderBadge != null)
            {
                BinderBadge.Height = Math.Max(12, iconSize * 0.45);
            }
        }

        private static void OnItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ItemCard card && e.NewValue is MunyuItem item)
            {
                card.DataContext = item;
                card.UpdateVisualState();

                item.PropertyChanged -= card.Item_PropertyChanged;
                item.PropertyChanged += card.Item_PropertyChanged;
            }
        }

        private void Item_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MunyuItem.IsSelected) ||
                e.PropertyName == nameof(MunyuItem.Children) ||
                e.PropertyName == nameof(MunyuItem.Type))
            {
                UpdateVisualState();
            }
        }

        private void UpdateVisualState()
        {
            if (Item == null) return;

            // Highlight border when selected
            CardBorder.BorderBrush = Item.IsSelected
                ? new SolidColorBrush(Color.FromRgb(0xA8, 0x55, 0xF7)) // Bright Purple Accent
                : Brushes.Transparent;

            // Binder badge visibility
            if (Item.Type == "binder")
            {
                BinderBadge.Visibility = Visibility.Visible;
                BinderCountText.Text = Item.ChildCount.ToString();
            }
            else
            {
                BinderBadge.Visibility = Visibility.Collapsed;
            }
        }

        private void ItemCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isMouseDown = true;
            _dragStartPoint = e.GetPosition(this);
            ItemSelected?.Invoke(this, Item!);

            // Double click on binder item expands it!
            if (e.ClickCount == 2 && Item != null && Item.Type == "binder")
            {
                BinderExpandRequested?.Invoke(this, Item);
                e.Handled = true;
            }
        }

        private void ItemCard_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isMouseDown = false;
        }

        private void ItemCard_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isMouseDown || e.LeftButton != MouseButtonState.Pressed || Item == null)
                return;

            Point currentPos = e.GetPosition(this);
            Vector diff = _dragStartPoint - currentPos;

            if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                _isMouseDown = false;
                StartDragOut();
            }
        }

        private void StartDragOut()
        {
            if (Item == null) return;

            // 1. Existence Validation before drag out (Section 6.2)
            if (Item.Type == "file")
            {
                if (!File.Exists(Item.Content) && !Directory.Exists(Item.Content))
                {
                    SystemSounds.Beep.Play();
                    ItemDeleteRequested?.Invoke(this, Item);
                    return;
                }
            }
            else if (Item.Type == "binder")
            {
                bool anyMissing = false;
                if (Item.Children != null)
                {
                    foreach (var child in Item.Children)
                    {
                        if (child.Type == "file" && !File.Exists(child.Content) && !Directory.Exists(child.Content))
                        {
                            anyMissing = true;
                            break;
                        }
                    }
                }

                if (anyMissing)
                {
                    SystemSounds.Beep.Play();
                    ItemDeleteRequested?.Invoke(this, Item);
                    return;
                }
            }

            // 2. Prepare DataObject for Drag & Drop Output
            var dataObject = new DataObject();

            // Mark as internal Munyu drag to disable island-to-island drop
            dataObject.SetData("Munyu_Internal_Item_Drag", true);

            if (Item.Type == "file")
            {
                string[] files = new[] { Item.Content };
                dataObject.SetData(DataFormats.FileDrop, files);
            }
            else if (Item.Type == "binder")
            {
                var fileList = new List<string>();
                if (Item.Children != null)
                {
                    foreach (var child in Item.Children)
                    {
                        if (child.Type == "file" && (File.Exists(child.Content) || Directory.Exists(child.Content)))
                        {
                            fileList.Add(child.Content);
                        }
                    }
                }
                if (fileList.Count > 0)
                {
                    dataObject.SetData(DataFormats.FileDrop, fileList.ToArray());
                }
                else
                {
                    return;
                }
            }
            else if (Item.Type == "url" || Item.Type == "text")
            {
                dataObject.SetData(DataFormats.Text, Item.Content);
                dataObject.SetData(DataFormats.UnicodeText, Item.Content);
            }

            // Execute native OS DoDragDrop
            DragDropEffects result = DragDrop.DoDragDrop(this, dataObject, DragDropEffects.Copy | DragDropEffects.Move | DragDropEffects.Link);

            // Hide window after drag out completes (Section 5.1)
            DragOutCompleted?.Invoke(this, EventArgs.Empty);
        }

        private void ItemCard_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (Item == null) return;

            var menu = new ContextMenuStrip();

            if (Item.Type == "binder")
            {
                menu.Items.Add("Unpack", null, (s, ev) => BinderUnpackRequested?.Invoke(this, Item));
                menu.Items.Add(new ToolStripSeparator());
            }

            menu.Items.Add("Delete", null, (s, ev) => ItemDeleteRequested?.Invoke(this, Item));

            var point = System.Windows.Forms.Cursor.Position;
            menu.Show(point);
        }
    }
}
