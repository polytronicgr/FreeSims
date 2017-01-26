﻿/*This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
If a copy of the MPL was not distributed with this file, You can obtain one at
http://mozilla.org/MPL/2.0/.

The Original Code is the TSOVille.

The Initial Developer of the Original Code is
ddfczm. All Rights Reserved.

Contributor(s): ______________________________________.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TSOVille.Code.UI.Framework;
using Microsoft.Xna.Framework;
using TSOVille.Code.UI.Framework.Parser;
using Microsoft.Xna.Framework.Graphics;

namespace TSOVille.Code.UI.Controls
{
    public class UIGridViewer : UIContainer
    {
        public event ChangeDelegate OnChange;

        /// <summary>
        /// Class to use as the item renderer for each cell in the grid
        /// </summary>
        public Type ItemRender = typeof(UIGridViewerRender);

        /// <summary>
        /// Grid of controls, we only create as many as we need
        /// for 1 page and then reuse them
        /// </summary>
        private UIGridViewerRender[,] Renders;


        private List<object> m_DataProvider;
        /// <summary>
        /// Sets the items within the list
        /// </summary>
        public virtual List<object> DataProvider
        {
            get
            {
                return m_DataProvider;
            }
            set
            {
                m_DataProvider = value;
                m_SelectedIndex = -1;
                Render();
            }
        }


        /**
         * Although rows & columns makes sense, the UIScripts
         * set sizes & the control calculates the rows and columns
         * so i guess we should do the same :S
         * 
         *  size				=	(326,210)
			thumbSize			=	(39,44)
			thumbMargins		=	(6,8)
			thumbImageSize		=	(33,33)
		    thumbImageOffsets	=	(2,2)
         */



        private int myRows;
        private int myColumns;

        [UIAttribute("thumbSize")]
        public Vector2 ThumbSize { get; set; }

        [UIAttribute("thumbMargins")]
        public Vector2 ThumbMargins { get; set; }

        [UIAttribute("thumbImageSize")]
        public Vector2 ThumbImageSize { get; set; }

        [UIAttribute("thumbImageOffsets")]
        public Vector2 ThumbImageOffsets { get; set; }

        [UIAttribute("size")]
        public new virtual Vector2 Size { get; set; }

        [UIAttribute("thumbButtonImage")]
        public Texture2D ThumbButtonImage { get; set; }


        public int ItemsPerPage
        {
            get
            {
                return myRows * myColumns;
            }
        }


        public virtual Vector2 GetGridArea()
        {
            return new Vector2(Size.X, Size.Y);
        }

        /// <summary>
        /// Once all the properties are set this will create
        /// the UI. Values such as size, thumb size etc can not
        /// be changed after this
        /// </summary>
        public virtual void Init()
        {
            /**
             * Cleanup
             */
            if (Renders != null)
            {
                for (int y = 0; y < Renders.GetLength(0); y++)
                {
                    for (int x = 0; x < Renders.GetLength(1); x++)
                    {
                        this.Remove(Renders[y, x]);
                    }
                }
            }

            var spanX = ThumbSize.X + ThumbMargins.X;
            var spanY = ThumbSize.Y + ThumbMargins.Y;

            var gridArea = GetGridArea();

            myColumns = (int)Math.Floor(gridArea.X / spanX);
            myRows = (int)Math.Floor(gridArea.Y / spanY);

            Renders = new UIGridViewerRender[myRows, myColumns];
            for (var y = 0; y < myRows; y++)
            {
                for (var x = 0; x < myColumns; x++)
                {
                    var newUIControl = (UIGridViewerRender)Activator.CreateInstance(ItemRender, new object[] { this });
                    newUIControl.X = ThumbMargins.X + (spanX * x);
                    newUIControl.Y = ThumbMargins.Y + (spanY * y);
                    this.Add(newUIControl);
                    Renders[y, x] = newUIControl;
                }
            }
        }


        private int m_SelectedPage;
        public int SelectedPage
        {
            get
            {
                return m_SelectedPage;
            }
            set
            {
                var maxPage = Math.Ceiling((double)m_DataProvider.Count / (double)ItemsPerPage);
                m_SelectedPage = value;
                m_SelectedPage = (int)Math.Min(m_SelectedPage, maxPage);
                m_SelectedPage = (int)Math.Max(m_SelectedPage, 0);

                Render();
            }
        }


        private int m_SelectedIndex;
        public object SelectedItem
        {
            get
            {
                if (m_SelectedIndex == -1)
                {
                    return null;
                }
                return m_DataProvider[m_SelectedIndex];
            }
            set
            {
                SelectedIndex = m_DataProvider.IndexOf(value);
                if (OnChange != null)
                {
                    OnChange(this);
                }
            }
        }


        public int SelectedIndex
        {
            get
            {
                return m_SelectedIndex;
            }
            set
            {
                var oldCell = GetCellForItemIndex(m_SelectedIndex);
                if (oldCell != null)
                {
                    oldCell.SetSelected(false);
                }
                m_SelectedIndex = value;
                var cell = GetCellForItemIndex(m_SelectedIndex);
                if (cell != null)
                {
                    cell.SetSelected(true);
                }
            }
        }

        /// <summary>
        /// Gets the cell that is currently rendering a given item
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        private UIGridViewerRender GetCellForItemIndex(int index)
        {
            var offset = m_SelectedPage * ItemsPerPage;
            if (index < offset || index > offset + ItemsPerPage)
            {
                /** Not on screen **/
                return null;
            }
            var localIndex = index - offset;
            var localY = (int)Math.Floor((double)localIndex / (double)myColumns);
            var localX = localIndex - (localY * myColumns);

            return Renders[localY, localX];
        }


        /// <summary>
        /// Draws the correct item in the correct slot
        /// </summary>
        protected void Render()
        {
            if (m_DataProvider == null) { return; }

            var offset = m_SelectedPage * ItemsPerPage;
            var perPage = myRows * myColumns;

            for (var i = 0; i < perPage; i++)
            {
                var y = (int)Math.Floor((double)i / (double)myColumns);
                var x = i - (y * myColumns);

                var ui = Renders[y, x];
                var itemIndex = offset + i;
                ui.SetSelected(false);

                if (itemIndex < m_DataProvider.Count)
                {
                    ui.SetData(m_DataProvider[itemIndex]);
                    ui.Visible = true;
                }
                else
                {
                    ui.Visible = false;
                }
            }

            SelectedIndex = m_SelectedIndex;
        }

    }
}