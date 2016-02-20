﻿using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace NFirmwareEditor.UI
{
	public sealed class PixelGrid : ScrollableControl
	{
		private bool[,] m_data = new bool[0, 0];
		private int m_blockSize = 50;
		private int m_colsCount;
		private int m_rowsCount;
		private Pen m_blockOuterBorderPen = Pens.DarkGray;
		private Pen m_blockInnerBorderPen = new Pen(Color.LightGray) { DashStyle = DashStyle.Dash };

		private Rectangle m_clientArea;
		private bool m_showGrid;

		public PixelGrid()
		{
			AutoScroll = true;
			SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
			SetStyle(ControlStyles.Selectable, false);
			UpdateStyles();
		}

		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public bool[,] Data
		{
			get { return m_data; }
			set
			{
				m_data = value;
				m_colsCount = Data.GetLength(0);
				m_rowsCount = Data.GetLength(1);
				CalculateSurfaceArea();
				Invalidate();
			}
		}

		public int BlockSize
		{
			get { return m_blockSize; }
			set
			{
				m_blockSize = value;
				CalculateSurfaceArea();
				Invalidate();
			}
		}

		public bool ShowGrid
		{
			get { return m_showGrid; }
			set
			{
				m_showGrid = value;
				Invalidate();
			}
		}

		public Pen BlockOuterBorderPen
		{
			get { return m_blockOuterBorderPen; }
			set
			{
				m_blockOuterBorderPen = value;
				Invalidate();
			}
		}

		public Pen BlockInnerBorderPen
		{
			get { return m_blockInnerBorderPen; }
			set
			{
				m_blockInnerBorderPen = value;
				Invalidate();
			}
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			var gfx = e.Graphics;
			{
				gfx.Clear(Color.White);
				gfx.TranslateTransform(-HorizontalScroll.Value, -VerticalScroll.Value);
				gfx.TranslateTransform(Margin.Left, Margin.Top);

				DrawBlocks(gfx);
				DrawGrid(gfx);
				DrawBorder(gfx);

				gfx.ResetTransform();
			}
		}

		protected override void OnResize(EventArgs e)
		{
			base.OnResize(e);
			Invalidate();
		}

		protected override void OnScroll(ScrollEventArgs se)
		{
			Invalidate();
		}

		protected override void OnMouseDown(MouseEventArgs e)
		{
			if (TrySetBlockValue(e)) Invalidate();
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			if (TrySetBlockValue(e)) Invalidate();
		}

		private void DrawBlocks(Graphics gfx)
		{
			for (var col = 0; col < m_colsCount; col++)
			{
				for (var row = 0; row < m_rowsCount; row++)
				{
					var blockRect = new Rectangle(col * BlockSize, row * BlockSize, BlockSize, BlockSize);
					gfx.FillRectangle(Data[col, row] ? Brushes.Black : Brushes.White, blockRect);
				}
			}
		}

		private void DrawGrid(Graphics gfx)
		{
			if (!m_showGrid) return;

			for (var row = 0; row < m_rowsCount; row++)
			{
				gfx.DrawLine(m_blockInnerBorderPen, 0, row * BlockSize, m_clientArea.Width - 1, row * BlockSize);
			}
			for (var col = 0; col < m_colsCount; col++)
			{
				gfx.DrawLine(m_blockInnerBorderPen, col * BlockSize, 0, col * BlockSize, m_clientArea.Height - 1);
			}
		}

		private void DrawBorder(Graphics gfx)
		{
			gfx.DrawRectangle(BlockOuterBorderPen, 0, 0, m_clientArea.Width - 1, m_clientArea.Height - 1);
		}

		private void CalculateSurfaceArea()
		{
			m_clientArea = new Rectangle(0, 0, m_colsCount * BlockSize, m_rowsCount * BlockSize);
			AutoScrollMinSize = new Size(m_clientArea.Width + Margin.Left + Margin.Right, m_clientArea.Height + Margin.Top + Margin.Bottom);
		}

		private Point GetRelativeMouseLocation(Point location)
		{
			return new Point(location.X - Margin.Left + HorizontalScroll.Value, location.Y - Margin.Top + VerticalScroll.Value);
		}

		private Point? TryGetBlockFromPoint(Point location)
		{
			var pos = GetRelativeMouseLocation(location);

			var blockCol = (int)Math.Floor((float)pos.X / BlockSize);
			var blockRow = (int)Math.Floor((float)pos.Y / BlockSize);

			if (blockCol > m_colsCount - 1 || blockCol < 0) return null;
			if (blockRow > m_rowsCount - 1 || blockRow < 0) return null;

			return new Point(blockCol, blockRow);
		}

		private bool TrySetBlockValue(MouseEventArgs e)
		{
			if (e.Button == MouseButtons.Left || e.Button == MouseButtons.Right)
			{
				var block = TryGetBlockFromPoint(e.Location);
				if (block == null) return false;

				Data[block.Value.X, block.Value.Y] = e.Button == MouseButtons.Left;
				return true;
			}
			return false;
		}
	}
}