/*
 * Copyright (C) 2013 47 Degrees, LLC
 *  http://47deg.com
 *  hello@47deg.com
 *
 *  Licensed under the Apache License, Version 2.0 (the "License");
 *  you may not use this file except in compliance with the License.
 *  You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0
 *
 *  Unless required by applicable law or agreed to in writing, software
 *  distributed under the License is distributed on an "AS IS" BASIS,
 *  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *  See the License for the specific language governing permissions and
 *  limitations under the License.
 */

using System;
using System.Collections.Generic;
using Android.Content;
using Android.Content.Res;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using Android.Support.V4.View;
using Android.Database;

namespace FortySevenDeg.SwipeListView
{
	public class SwipeListView : ListView
	{
        /* const */
        public const float DefaultSwipOffset = 2;

        /* enum */
		public enum SwipeAction
		{
			None = 0,
			Reveal = 1,
			Dismiss = 2,
			Choice = 3
		}

		public enum SwipeMode
		{
			Default = -1,
			None = 0,
			Both = 1,
			Right = 2,
			Left = 3
		}

		public enum TouchState
		{
			Rest = 0,
			ScrollingX = 1,
			ScrollingY = 2
		}

        /**
		 * Default ids for front view
		 */
        public static String SwipeDefaultFrontView = "swipelist_frontview";
        /**
		 * Default ids for back view
		 */

		public static String SwipeDefaultBackView = "swipelist_backview";

        /* private */
		private int _swipeBackView;
		private int _swipeFrontView;
		private SwipeListViewTouchListener _touchListener;
		private float _lastMotionX;
		private float _lastMotionY;
		private int _touchSlop;
		private TouchState _touchState = TouchState.Rest;
       
		public SwipeListView(Context context, IAttributeSet attrs) : base(context, attrs)
		{
			Init(attrs);
		}

		public SwipeListView(Context context, IAttributeSet attrs, int swipeBackView, int swipeFrontView) : base(context, attrs)
		{
			_swipeFrontView = swipeFrontView;
			_swipeBackView = swipeBackView;
			Init(attrs);
		}

		public ISwipeListViewListener SwipeListViewListener { get; set; }

		private void Init(IAttributeSet attrs)
		{
			long swipeAnimationTime = 0;
			float swipeOffsetLeft = 0;
			float swipeOffsetRight = 0;
			var swipeMode = (int) SwipeMode.Both;
			bool swipeOpenOnLongPress = true;
			bool swipeCloseAllItemsWhenMoveList = true;
			int swipeDrawableChecked = 0;
			int swipeDrawableUnchecked = 0;
            float swipeOffset = DefaultSwipOffset;
			var swipeActionLeft = (int) SwipeAction.Reveal;
			var swipeActionRight = (int) SwipeAction.Reveal;

			if (attrs != null)
			{
				TypedArray styled = Context.ObtainStyledAttributes(attrs, Resource.Styleable.SwipeListView);
				swipeMode = styled.GetInt(Resource.Styleable.SwipeListView_swipeMode, ((int) SwipeMode.Both));
				swipeActionLeft = styled.GetInt(Resource.Styleable.SwipeListView_swipeActionLeft, (int) SwipeAction.Reveal);
				swipeActionRight = styled.GetInt(Resource.Styleable.SwipeListView_swipeActionRight, (int) SwipeAction.Reveal);
                swipeOffset = styled.GetFloat(Resource.Styleable.SwipeListView_swipeOffset, swipeOffset);
				swipeOffsetLeft = styled.GetDimension(Resource.Styleable.SwipeListView_swipeOffsetLeft, 0);
				swipeOffsetRight = styled.GetDimension(Resource.Styleable.SwipeListView_swipeOffsetRight, 0);
				swipeOpenOnLongPress = styled.GetBoolean(Resource.Styleable.SwipeListView_swipeOpenOnLongPress, swipeOpenOnLongPress);
				swipeAnimationTime = styled.GetInt(Resource.Styleable.SwipeListView_swipeAnimationTime, 0);
				swipeCloseAllItemsWhenMoveList = styled.GetBoolean(Resource.Styleable.SwipeListView_swipeCloseAllItemsWhenMoveList, true);
				swipeDrawableChecked = styled.GetResourceId(Resource.Styleable.SwipeListView_swipeDrawableChecked, 0);
				swipeDrawableUnchecked = styled.GetResourceId(Resource.Styleable.SwipeListView_swipeDrawableUnchecked, 0);
				_swipeFrontView = styled.GetResourceId(Resource.Styleable.SwipeListView_swipeFrontView, 0);
				_swipeBackView = styled.GetResourceId(Resource.Styleable.SwipeListView_swipeBackView, 0);
			}

			if (_swipeFrontView == 0 || _swipeBackView == 0)
			{
				_swipeFrontView = Context.Resources.GetIdentifier(SwipeDefaultFrontView, "id", Context.PackageName);
				_swipeBackView = Context.Resources.GetIdentifier(SwipeDefaultBackView, "id", Context.PackageName);

				if (_swipeFrontView == 0 || _swipeBackView == 0)
				{
					throw new Exception(
						String.Format(
							"You forgot the attributes swipeFrontView or swipeBackView. You can add these attributes or use '{0}' and '{1}' identifiers",
							SwipeDefaultFrontView, SwipeDefaultBackView));
				}
			}

			ViewConfiguration configuration = ViewConfiguration.Get(Context);
			_touchSlop = ViewConfigurationCompat.GetScaledPagingTouchSlop(configuration);
			_touchListener = new SwipeListViewTouchListener(this, _swipeFrontView, _swipeBackView);

			if (swipeAnimationTime > 0)
			{
				_touchListener.AnimationTime = swipeAnimationTime;
			}
			_touchListener.RightOffset = swipeOffsetRight;
			_touchListener.LeftOffset = swipeOffsetLeft;
            _touchListener.SwipeOffset = swipeOffset;
			_touchListener.SwipeActionLeft = swipeActionLeft;
			_touchListener.SwipeActionRight = swipeActionRight;
			_touchListener.SwipeMode = swipeMode;
			_touchListener.SwipeClosesAllItemsWhenListMoves = swipeCloseAllItemsWhenMoveList;
			_touchListener.SwipeOpenOnLongPress = swipeOpenOnLongPress;
			_touchListener.SwipeDrawableChecked = swipeDrawableChecked;
			_touchListener.SwipeDrawableUnchecked = swipeDrawableUnchecked;
			SetOnTouchListener(_touchListener);
			SetOnScrollListener(_touchListener);
		}

		/// <summary>
		///     Recycle the specified convertView and position.
		/// </summary>
		/// <param name="convertView">Convert view.</param>
		/// <param name="position">Position.</param>
		public void Recycle(View convertView, int position)
		{
			_touchListener.ReloadChoiceStateInView(convertView.FindViewById(_swipeFrontView), position);
			_touchListener.ReloadSwipeStateInView(convertView.FindViewById(_swipeFrontView), position);

			// Clean pressed state (if dismiss is fire from a cell, to this cell, with a press drawable, in a swipelistview
			// when this cell will be recycle it will still have his pressed state. This ensure the pressed state is
			// cleaned.
			for (int j = 0; j < ((ViewGroup) convertView).ChildCount; ++j)
			{
				View nextChild = ((ViewGroup) convertView).GetChildAt(j);
				nextChild.Pressed = false;
			}
		}

		/// <summary>
		///     Ises the checked.
		/// </summary>
		/// <returns>The checked.</returns>
		/// <param name="position">Position.</param>
		public bool IsChecked(int position)
		{
			return _touchListener.IsChecked(position);
		}

		public void ResetScrolling()
		{
			_touchState = TouchState.Rest;
		}

		/// <summary>
		///     Sets the offset right.
		/// </summary>
		/// <param name="offsetRight">Offset right.</param>
		public void SetOffsetRight(float offsetRight)
		{
			_touchListener.RightOffset = offsetRight;
		}

		/// <summary>
		///     Sets the offset left.
		/// </summary>
		/// <param name="offsetLeft">Offset left.</param>
		public void SetOffsetLeft(float offsetLeft)
		{
			_touchListener.LeftOffset = offsetLeft;
		}

		/// <summary>
		///     Sets the swipe close all items when move list.
		/// </summary>
		/// <param name="swipeCloseAllItemsWhenMoveList">Swipe close all items when move list.</param>
		public void SetSwipeCloseAllItemsWhenMoveList(bool swipeCloseAllItemsWhenMoveList)
		{
			_touchListener.SwipeClosesAllItemsWhenListMoves = swipeCloseAllItemsWhenMoveList;
		}

		/// <summary>
		///     Sets the swipe open on long press.
		/// </summary>
		/// <param name="swipeOpenOnLongPress">Swipe open on long press.</param>
		public void SetSwipeOpenOnLongPress(bool swipeOpenOnLongPress)
		{
			_touchListener.SwipeOpenOnLongPress = swipeOpenOnLongPress;
		}

		/// <summary>
		///     Sets the swipe mode.
		/// </summary>
		/// <param name="swipeMode">Swipe mode.</param>
		public void SetSwipeMode(int swipeMode)
		{
			_touchListener.SwipeMode = swipeMode;
		}

		/// <summary>
		///     Gets the swipe action left.
		/// </summary>
		/// <returns>The swipe action left.</returns>
		public int GetSwipeActionLeft()
		{
			return _touchListener.SwipeActionLeft;
		}

		/// <summary>
		///     Sets the swipe action left.
		/// </summary>
		/// <param name="swipeActionLeft">Swipe action left.</param>
		public void SetSwipeActionLeft(int swipeActionLeft)
		{
			_touchListener.SwipeActionLeft = swipeActionLeft;
		}

		/// <summary>
		///     Gets the swipe action right.
		/// </summary>
		/// <returns>The swipe action right.</returns>
		public int GetSwipeActionRight()
		{
			return _touchListener.SwipeActionRight;
		}

		/// <summary>
		///     Sets the swipe action right.
		/// </summary>
		/// <param name="swipeActionRight">Swipe action right.</param>
		public void SetSwipeActionRight(int swipeActionRight)
		{
			_touchListener.SwipeActionRight = swipeActionRight;
		}

		/// <summary>
		///     Sets the animation time.
		/// </summary>
		/// <param name="animationTime">Animation time.</param>
		public void SetAnimationTime(long animationTime)
		{
			_touchListener.AnimationTime = animationTime;
		}

		/// <summary>
		///     Initializes a new instance of the <see cref="CegiSwipeListView" /> class.
		/// </summary>
		/// <param name="ev">Ev.</param>
		public override bool OnInterceptTouchEvent(MotionEvent ev)
		{
			int action = MotionEventCompat.GetActionMasked(ev);
			float x = ev.GetX();
			float y = ev.GetY();

			if (Enabled && _touchListener.IsSwipeEnabled)
			{
				if (_touchState == TouchState.ScrollingX)
				{
					return _touchListener.OnTouch(this, ev);
				}

				switch (action)
				{
					case (int) MotionEventActions.Move:
						CheckInMoving(x, y);
						return _touchState == TouchState.ScrollingY;
					case (int) MotionEventActions.Down:
						base.OnInterceptTouchEvent(ev);
						_touchListener.OnTouch(this, ev);
						_touchState = TouchState.Rest;
						_lastMotionX = x;
						_lastMotionY = y;
						return false;
					case (int) MotionEventActions.Cancel:
						_touchState = TouchState.Rest;
						break;
					case (int) MotionEventActions.Up:
						_touchListener.OnTouch(this, ev);
						return _touchState == TouchState.ScrollingY;
					default:
						break;
				}
			}

			return base.OnInterceptTouchEvent(ev);
		}

		/// <summary>
		///     Checks the in moving.
		/// </summary>
		/// <param name="x">The x coordinate.</param>
		/// <param name="y">The y coordinate.</param>
		private void CheckInMoving(float x, float y)
		{
			var xDiff = (int) Math.Abs(x - _lastMotionX);
			var yDiff = (int) Math.Abs(y - _lastMotionY);

            bool xMoved = xDiff > _touchSlop;
            bool yMoved = yDiff > _touchSlop;

			if (xMoved)
			{
				_touchState = TouchState.ScrollingX;
				_lastMotionX = x;
				_lastMotionY = y;
			}

			if (yMoved)
			{
				_touchState = TouchState.ScrollingY;
				_lastMotionX = x;
				_lastMotionY = y;
			}
		}

		/// <summary>
		///     Closes the opened items.
		/// </summary>
		public void CloseOpenedItems()
		{
			_touchListener.CloseOpenedItems();
		}

		public void Close(int position)
		{
			_touchListener.CloseAnimate(position);
		}

		public void Open(int position)
		{
			_touchListener.OpenAnimate(position);
		}

		#region notification events/listener

		/// Notifies OnDismiss
		public event EventHandler<OnDismissEventArgs> OnDismissEvent;

		public class OnDismissEventArgs : EventArgs
		{
			public int[] ReverseSortedPositions { get; set; }
		}

		public void OnDismiss(int[] reverseSortedPositions)
		{
			if (SwipeListViewListener != null) SwipeListViewListener.OnDismiss(reverseSortedPositions);
			if (OnDismissEvent != null) OnDismissEvent(this, new OnDismissEventArgs { ReverseSortedPositions = reverseSortedPositions });
		}

		/// Notifies OnStartOpen
		public event EventHandler<OnStartOpenEventArgs> OnStartOpenEvent;

		public class OnStartOpenEventArgs : EventArgs
		{
			public int Position { get; set; }
			public int Action { get; set; }
			public bool Right { get; set; }
		}

		public void OnStartOpen(int position, int action, bool right)
		{
			if (position != InvalidPosition)
			{
				if (SwipeListViewListener != null) SwipeListViewListener.OnStartOpen(position, action, right);

				if (OnStartOpenEvent != null)
					OnStartOpenEvent(this, new OnStartOpenEventArgs
					{
						Position = position,
						Action = action,
						Right = right
					});
			}
		}

		/// Notifies OnStartClose
		public event EventHandler<OnStartCloseEventArgs> OnStartCloseEvent;

		public class OnStartCloseEventArgs : EventArgs
		{
			public int Position { get; set; }
			public bool Right { get; set; }
		}

		public void OnStartClose(int position, bool right)
		{
			if (position != InvalidPosition)
			{
				if (SwipeListViewListener != null) SwipeListViewListener.OnStartClose(position, right);
				if (OnStartCloseEvent != null) OnStartCloseEvent(this, new OnStartCloseEventArgs { Position = position, Right = right });
			}
		}

		/// Notifies OnClickFrontView
		public event EventHandler<OnClickEventArgs> OnClickFrontViewEvent;

		public class OnClickEventArgs : EventArgs
		{
			public int Position { get; set; }
		}

		public void OnClickFrontView(int position)
		{
			if (position != InvalidPosition)
			{
				if (SwipeListViewListener != null) SwipeListViewListener.OnClickFrontView(position);
				if (OnClickFrontViewEvent != null) OnClickFrontViewEvent(this, new OnClickEventArgs { Position = position });
			}
		}

		/// Notifies OnClickBackView
		public event EventHandler<OnClickEventArgs> OnClickBackViewEvent;

		public void OnClickBackView(int position)
		{
			if (position != InvalidPosition)
			{
				if (SwipeListViewListener != null) SwipeListViewListener.OnClickBackView(position);
				if (OnClickBackViewEvent != null) OnClickBackViewEvent(this, new OnClickEventArgs { Position = position });
			}
		}

		/// Notifies OnOpened
		public event EventHandler<OnOpenedEventArgs> OnOpenedEvent;

		public class OnOpenedEventArgs : EventArgs
		{
			public int Position { get; set; }
			public bool ToRight { get; set; }
		}

		public void OnOpened(int position, bool toRight)
		{
			if (position != InvalidPosition)
			{
				if (SwipeListViewListener != null) SwipeListViewListener.OnOpened(position, toRight);
				if (OnOpenedEvent != null) OnOpenedEvent(this, new OnOpenedEventArgs { Position = position, ToRight = toRight });
			}
		}

		/// Notifies OnClosed
		public event EventHandler<OnClosedEventArgs> OnClosedEvent;

		public class OnClosedEventArgs : EventArgs
		{
			public int Position { get; set; }
			public bool FromRight { get; set; }
		}

		public void OnClosed(int position, bool fromRight)
		{
			if (position != InvalidPosition)
			{
				if (SwipeListViewListener != null) SwipeListViewListener.OnClosed(position, fromRight);
				if (OnClosedEvent != null) OnClosedEvent(this, new OnClosedEventArgs { Position = position, FromRight = fromRight });
			}
		}

		/// Notifies OnChoiceChanged
		public event EventHandler<OnChoiceChangedEventArgs> OnChoiceChangedEvent;

		public class OnChoiceChangedEventArgs : EventArgs
		{
			public int Position { get; set; }
			public bool Selected { get; set; }
		}

		public void OnChoiceChanged(int position, bool selected)
		{
			if (position != InvalidPosition)
			{
				if (SwipeListViewListener != null) SwipeListViewListener.OnChoiceChanged(position, selected);
				if (OnChoiceChangedEvent != null) OnChoiceChangedEvent(this, new OnChoiceChangedEventArgs { Position = position, Selected = selected });
			}
		}

		/// Notifies OnChoiceStarted
		public event EventHandler<EventArgs> OnChoiceStartedEvent;

		public void OnChoiceStarted()
		{
			if (SwipeListViewListener != null) SwipeListViewListener.OnChoiceStarted();
			if (OnChoiceStartedEvent != null) OnChoiceStartedEvent(this, EventArgs.Empty);
		}

		/// Notifies OnChoiceEnded
		public event EventHandler<EventArgs> OnChoiceEndedEvent;

		public void OnChoiceEnded()
		{
			if (SwipeListViewListener != null) SwipeListViewListener.OnChoiceEnded();
			if (OnChoiceEndedEvent != null) OnChoiceEndedEvent(this, EventArgs.Empty);
		}

		/// Notifies OnFirstListItem
		public event EventHandler<EventArgs> OnFirstListItemEvent;

		public void OnFirstListItem()
		{
			if (SwipeListViewListener != null) SwipeListViewListener.OnFirstListItem();
			if (OnFirstListItemEvent != null) OnFirstListItemEvent(this, EventArgs.Empty);
		}

		/// Notifies OnLastListItem
		public event EventHandler<EventArgs> OnLastListItemEvent;

		public void OnLastListItem()
		{
			if (SwipeListViewListener != null) SwipeListViewListener.OnLastListItem();
			if (OnLastListItemEvent != null) OnLastListItemEvent(this, EventArgs.Empty);
		}

		/// Notifies OnListChanged
		public event EventHandler<EventArgs> OnListChangedEvent;

		public void OnListChanged()
		{
			if (SwipeListViewListener != null) SwipeListViewListener.OnListChanged();
			if (OnListChangedEvent != null) OnListChangedEvent(this, EventArgs.Empty);
		}

		/// Notifies OnMove
		public event EventHandler<OnMoveEventArgs> OnMoveEvent;

		public class OnMoveEventArgs : EventArgs
		{
			public int Position { get; set; }
			public float PosX { get; set; }
		}

		public void OnMove(int position, float x)
		{
			if (position != InvalidPosition)
			{
				if (SwipeListViewListener != null) SwipeListViewListener.OnMove(position, x);
				if (OnMoveEvent != null) OnMoveEvent(this, new OnMoveEventArgs { Position = position, PosX = x });
			}
		}

		/// Notifies ChangeSwipeMode
		public event EventHandler<ChangeSwipeModeEventArgs> ChangeSwipeModeEvent;

		public class ChangeSwipeModeEventArgs : EventArgs
		{
			public int Position { get; set; }
		}

		public int ChangeSwipeMode(int position)
		{
			if (position != InvalidPosition)
			{
				if (ChangeSwipeModeEvent != null) ChangeSwipeModeEvent(this, new ChangeSwipeModeEventArgs { Position = position });
				if (SwipeListViewListener != null) return SwipeListViewListener.OnChangeSwipeMode(position);
			}
			return (int)SwipeMode.Default;
		}

		#endregion

		#region "Properties"

		/// <summary>
		///     Gets the positions selected.
		/// </summary>
		/// <returns>The positions selected.</returns>
		public List<int> PositionsSelected
		{
			get { return _touchListener.PositionsSelected; }
		}

		/// <summary>
		///     Gets the count selected.
		/// </summary>
		/// <returns>The count selected.</returns>
		public int CountSelected
		{
			get { return _touchListener.CountSelected; }
		}

		/// <summary>
		///     Gets or sets the adapter.
		/// </summary>
		/// <value>The adapter.</value>
		public new IListAdapter Adapter
		{
			get { return base.Adapter; }
			set
			{
				base.Adapter = value;
				_touchListener.ResetItems();

				if (base.Adapter != null)
				{
					var observer = new ObjectDataSetObserver
					{
					    Changed = () =>
					    {
					        OnListChanged();
					        _touchListener.ResetItems();
					    }
					};

				    base.Adapter.RegisterDataSetObserver(observer);
				}
			}
		}

		/// <summary>
		///     Unselecteds the choice states.
		/// </summary>
		public void UnselectedChoiceStates()
		{
			_touchListener.UnselectedChoiceStates();
		}


		/// <summary>
		///     Dismiss the specified position.
		/// </summary>
		/// <param name="position">Position.</param>
		public void Dismiss(int position)
		{
			int height = _touchListener.Dismiss(position);
			if (height > 0)
			{
				_touchListener.HandlerPendingDismisses(height);
			}
			else
			{
				var dismissPositions = new int[1];
				dismissPositions[0] = position;
				OnDismiss(dismissPositions);
				_touchListener.ResetPendingDismisses();
			}
		}

		/// <summary>
		///     Dismisses items selected.
		/// </summary>
		public void DismissSelected()
		{
			List<int> list = _touchListener.PositionsSelected;
			var dismissPositions = new int[list.Count];
			int height = 0;
			for (int i = 0; i < list.Count; i++)
			{
				int position = list[i];
				dismissPositions[i] = position;
				int auxHeight = _touchListener.Dismiss(position);
				if (auxHeight > 0)
				{
					height = auxHeight;
				}
			}
			if (height > 0)
			{
				_touchListener.HandlerPendingDismisses(height);
			}
			else
			{
				OnDismiss(dismissPositions);
				_touchListener.ResetPendingDismisses();
			}
			_touchListener.ReturnOldActions();
		}

		/// <summary>
		///     Opens the animate.
		/// </summary>
		/// <param name="position">Position.</param>
		public void OpenAnimate(int position)
		{
			_touchListener.OpenAnimate(position);
		}

		/// <summary>
		///     Closes the animate.
		/// </summary>
		/// <param name="position">Position.</param>
		public void CloseAnimate(int position)
		{
			_touchListener.CloseAnimate(position);
		}

		#endregion
	}

	public class ObjectDataSetObserver : DataSetObserver
	{
		public Action Changed { get; set; }

		public override void OnChanged()
		{
			Changed();
		}
	}
}