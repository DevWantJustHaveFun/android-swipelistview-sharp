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
using System.Linq;

using Android.Graphics;
using Android.OS;
using Android.Util;
using Android.Views;
using Android.Widget;
using Android.Support.V4.View;
using Android.Animation;
using System.Threading.Tasks;

namespace FortySevenDeg.SwipeListView
{
	public class SwipeListViewTouchListener : Java.Lang.Object, View.IOnTouchListener, AbsListView.IOnScrollListener
	{
		/*  const */
		private const int DisplaceChoice = 80;

		/* private readonly */
		private readonly Rect _rect = new Rect();       
		private readonly List<PendingDismissData> _pendingDismisses = new List<PendingDismissData>();
		private readonly List<BackViewHolder> _backViews = new List<BackViewHolder>();
		private readonly Dictionary<int, bool> _checked = new Dictionary<int, bool>();		
		private readonly Dictionary<int, bool> _opened = new Dictionary<int, bool>();
		private readonly Dictionary<int, bool> _openedRight = new Dictionary<int, bool>();
		private readonly int _slop;
		private readonly int _swipeBackView;
		private readonly int _swipeFrontView;
		private readonly int _maxFlingVelocity;
		private readonly int _minFlingVelocity;
		private readonly long _configShortAnimationTime;
		private readonly SwipeListView _swipeListView;

		/* private */
		private View _backView;
		private View _frontView;
		private VelocityTracker _velocityTracker;

		private bool _paused;
		private bool _isFirstItem;
		private bool _isLastItem;
		private bool _isLongPress;
		private bool _swiping;
		private bool _swipingRight;

		private int _viewWidth = 1; // 1 and not 0 to prevent dividing by zero
		private int _childPosition;
		private int _dismissAnimationRefCount;
		private int _downPosition;
		private int _oldSwipeActionLeft;
		private int _oldSwipeActionRight;
		private int _swipeCurrentAction = (int)SwipeListView.SwipeAction.None;
		private long _animationTime;
		private float _downX;
			
		/* constructor */
		public SwipeListViewTouchListener(SwipeListView swipeListView, int swipeFrontView, int swipeBackView)
		{
			/* set private */
			_swipeFrontView = swipeFrontView;
			_swipeBackView = swipeBackView;
			ViewConfiguration vc = ViewConfiguration.Get(swipeListView.Context);
			_slop = vc.ScaledTouchSlop;		
			_minFlingVelocity = vc.ScaledMinimumFlingVelocity;
			_maxFlingVelocity = vc.ScaledMaximumFlingVelocity;
			_configShortAnimationTime = swipeListView.Context.Resources.GetInteger(Android.Resource.Integer.ConfigShortAnimTime);
			_animationTime = _configShortAnimationTime;
			_swipeListView = swipeListView;

			/* set properites default value */
			SwipeClosesAllItemsWhenListMoves = true;
			SwipeOpenOnLongPress = true;
			SwipeMode = (int) SwipeListView.SwipeMode.Both;
			SwipeActionLeft = (int) SwipeListView.SwipeAction.Reveal;
			SwipeActionRight = (int) SwipeListView.SwipeAction.Reveal;
			LeftOffset = 0;
			RightOffset = 0;
			SwipeOffset = SwipeListView.DefaultSwipOffset;
			SwipeDrawableChecked = 0;
			SwipeDrawableUnchecked = 0;
		}

		#region "Properties"

		public View ParentView { get; set; }

		public View FrontView
		{
			get { return _frontView; }
			set
			{
				_frontView = value;

				if (_frontView != null)
				{
					_frontView.Click -= FrontViewClick;
					_frontView.Click += FrontViewClick;
					_frontView.LongClick -= FrontViewLongClick;
					_frontView.LongClick += FrontViewLongClick;
				}
			}
		}

		private View BackView
		{
			get { return _backView; }
			set
			{
				_backView = value;

				if (_backView != null)
				{
					_backView.Click -= BackViewClick;
					_backView.Click += BackViewClick;
				}
			}
		}

		public bool IsListViewMoving { get; set; }

		/// Sets animation time when the user drops the cell
		public long AnimationTime
		{
			get { return _animationTime; }
			set {
				_animationTime = value > 0 ? value : _configShortAnimationTime;
			}
		}

		public float SwipeOffset { get; set; }
		public float RightOffset { get; set; }
		public float LeftOffset { get; set; }

		/// Set if all items opened will be closed when the user moves ListView
		public bool SwipeClosesAllItemsWhenListMoves { get; set; }

		/// Set if the user can open an item with long press on cell
		public bool SwipeOpenOnLongPress { get; set; }

		public int SwipeMode { get; set; }

		public bool IsSwipeEnabled
		{
			get { return SwipeMode != (int) SwipeListView.SwipeMode.None; }
		}

		public int SwipeActionLeft { get; set; }
		public int SwipeActionRight { get; set; }
		public int SwipeDrawableChecked { get; set; }

		/// Set drawable unchecked (only SWIPE_ACTION_CHOICE)
		public int SwipeDrawableUnchecked { get; set; }

		public int CountSelected
		{
			get
			{
				int count = 0;
				for (int i = 0; i < _checked.Count; i++)
				{
					if (IsChecked(i))
					{
						count++;
					}
				}
#if DEBUG
				Log.Debug("SwipeListView", "selected: " + count);
#endif
				return count;
			}
		}

		/// <summary>
		///     Gets the positions selected.
		/// </summary>
		/// <returns>The positions selected.</returns>
		public List<int> PositionsSelected
		{
			get
			{
				var list = new List<int>();
				for (int i = 0; i < _checked.Count; i++)
				{
					if (IsChecked(i))
					{
						list.Add(i);
					}
				}
				return list;
			}
		}

		private void FrontViewClick(object sender, EventArgs e)
		{
			if (!_isLongPress)
			{
				_swipeListView.OnClickFrontView(_downPosition);
			}
			_isLongPress = false;
		}

		private void FrontViewLongClick(object sender, View.LongClickEventArgs e)
		{
			if (SwipeOpenOnLongPress)
			{
				if (_downPosition >= 0)
				{
					_isLongPress = true;
					OpenAnimate(_frontView, _childPosition);
				}
			}
			else
			{
				SwapChoiceState(_childPosition);
			}
		}

		private void BackViewClick(object sender, EventArgs e)
		{
			_swipeListView.OnClickBackView(_downPosition);
		}

		#endregion

		public bool OpenedRight(int position)
		{
			return _openedRight.ContainsKey(position) && _openedRight.FirstOrDefault(o => o.Key == position).Value;
		}

		public bool Opened(int position)
		{
			return _opened.ContainsKey(position) && _opened.FirstOrDefault(o => o.Key == position).Value;
		}


		public void ResetItems()
		{
			if (_swipeListView.Adapter != null)
			{
				int count = _swipeListView.Adapter.Count;
				for (int i = _opened.Count; i <= count; i++)
				{
					_opened[i] = false;
					_openedRight[i] = false;
					_checked[i] = false;
				}
			}
		}

		/// <summary>
		///     Slide open item
		/// </summary>
		/// <param name="position">Position.</param>
		public void OpenAnimate(int position)
		{
			View view =
				_swipeListView.GetChildAt(position - _swipeListView.FirstVisiblePosition).FindViewById(_swipeFrontView);
			if (view != null)
			{
				OpenAnimate(view, position);
			}
		}

		public void CloseAnimate(int position)
		{
			View view =
				_swipeListView.GetChildAt(position - _swipeListView.FirstVisiblePosition).FindViewById(_swipeFrontView);
			if (view != null)
			{
				CloseAnimate(view, position);
			}
		}

		/// <summary>
		///     Swaps the state of the choice.
		/// </summary>
		/// <param name="position">Position.</param>
		private void SwapChoiceState(int position)
		{
			int lastCount = CountSelected;
			bool lastChecked = IsChecked(position);
			_checked[position] = !lastChecked;
			int count = lastChecked ? lastCount - 1 : lastCount + 1;
			if (lastCount == 0 && count == 1)
			{
				_swipeListView.OnChoiceStarted();
				CloseOpenedItems();
				SetActionsTo((int) SwipeListView.SwipeAction.Choice);
			}
			if (lastCount == 1 && count == 0)
			{
				_swipeListView.OnChoiceEnded();
				ReturnOldActions();
			}
			if (Build.VERSION.SdkInt >= BuildVersionCodes.Honeycomb)
			{
				_swipeListView.SetItemChecked(position, !lastChecked);
			}
			_swipeListView.OnChoiceChanged(position, !lastChecked);
			ReloadChoiceStateInView(FrontView, position);
		}

		/// <summary>
		///     Unselecteds the choice states.
		/// </summary>
		public void UnselectedChoiceStates()
		{
			int start = _swipeListView.FirstVisiblePosition;
			int end = _swipeListView.LastVisiblePosition;
			for (int i = 0; i < _checked.Count; i++)
			{
				if (IsChecked(i) && i >= start && i <= end)
				{
					ReloadChoiceStateInView(_swipeListView.GetChildAt(i - start).FindViewById(_swipeFrontView), i);
				}
				_checked[i] = false;
			}
			_swipeListView.OnChoiceEnded();
			ReturnOldActions();
		}

		/// <summary>
		///     Dismiss the specified position.
		/// </summary>
		/// <param name="position">Position.</param>
		public int Dismiss(int position)
		{
			int start = _swipeListView.FirstVisiblePosition;
			int end = _swipeListView.LastVisiblePosition;
			View view = _swipeListView.GetChildAt(position - start);
			++_dismissAnimationRefCount;
			if (position >= start && position <= end)
			{
				PerformDismiss(view, position, false);
				return view.Height;
			}
			_pendingDismisses.Add(new PendingDismissData(position, null));
			return 0;
		}

		/// <summary>
		///     Reloads the choice state in view.
		/// </summary>
		/// <param name="frontView">Front view.</param>
		/// <param name="position">Position.</param>
		public void ReloadChoiceStateInView(View frontView, int position)
		{
			if (IsChecked(position))
			{
				if (SwipeDrawableChecked > 0) frontView.SetBackgroundResource(SwipeDrawableChecked);
			}
			else
			{
				if (SwipeDrawableUnchecked > 0) frontView.SetBackgroundResource(SwipeDrawableUnchecked);
			}
		}

		/// <summary>
		///     Reloads the swipe state in view.
		/// </summary>
		/// <param name="frontView">Front view.</param>
		/// <param name="position">Position.</param>
		public void ReloadSwipeStateInView(View frontView, int position)
		{
			if (!Opened(position))
			{
				frontView.TranslationX = 0f;
			}
			else
			{
				if (OpenedRight(position))
				{
					frontView.TranslationX = _swipeListView.Width;
				}
				else
				{
					frontView.TranslationX = -_swipeListView.Width;
				}
			}
		}

		/// <summary>
		///     Determines whether this instance is checked the specified position.
		/// </summary>
		/// <returns><c>true</c> if this instance is checked the specified position; otherwise, <c>false</c>.</returns>
		/// <param name="position">Position.</param>
		public bool IsChecked(int position)
		{
			return _checked.ContainsKey(position) && _checked.FirstOrDefault(o => o.Key == position).Value;
		}


		/// <summary>
		///     Gets the count selected.
		/// </summary>
		/// <returns>The count selected.</returns>
		protected int GetCountSelected()
		{
			int count = 0;
			for (int i = 0; i < _checked.Count; i++)
			{
				if (_checked[i])
				{
					count++;
				}
			}
#if DEBUG
			Log.Debug("CegiSwipeListView", "selected: " + count);
#endif
			return count;
		}

		/// <summary>
		///     Gets the positions selected.
		/// </summary>
		/// <returns>The positions selected.</returns>
		protected List<int> GetPositionsSelected()
		{
			var list = new List<int>();
			for (int i = 0; i < _checked.Count; i++)
			{
				if (_checked[i])
				{
					list.Add(i);
				}
			}
			return list;
		}


		private void OpenAnimate(View view, int position)
		{
			if (!Opened(position))
			{
				GenerateRevealAnimate(view, true, false, position);
			}
		}

		/// <summary>
		///     Closes the animate.
		/// </summary>
		/// <param name="view">View.</param>
		/// <param name="position">Position.</param>
		private void CloseAnimate(View view, int position)
		{
			if (Opened(position))
			{
				GenerateRevealAnimate(view, true, false, position);
			}
		}

		/// <summary>
		///     Generates the animate.
		/// </summary>
		/// <param name="view">View.</param>
		/// <param name="swap">If set to <c>true</c> swap.</param>
		/// <param name="swapRight">If set to <c>true</c> swap right.</param>
		/// <param name="position">Position.</param>
		private void GenerateAnimate(View view, bool swap, bool swapRight, int position)
		{
#if DEBUG
			Log.Debug("SwipeListView", "swap: " + swap + " - swapRight: " + swapRight + " - position: " + position + " - currentAction: " + _swipeCurrentAction);
#endif
			if (_swipeCurrentAction == (int) SwipeListView.SwipeAction.Reveal)
			{
				GenerateRevealAnimate(view, swap, swapRight, position);
			}
			if (_swipeCurrentAction == (int) SwipeListView.SwipeAction.Dismiss)
			{
				GenerateDismissAnimate(ParentView, swap, swapRight, position);
			}
			if (_swipeCurrentAction == (int) SwipeListView.SwipeAction.Choice)
			{
				GenerateChoiceAnimate(view, position);
			}
		}

		/// <summary>
		///     Generates the choice animate.
		/// </summary>
		/// <param name="view">View.</param>
		/// <param name="position">Position.</param>
		private void GenerateChoiceAnimate(View view, int position)
		{
			var listener = new ObjectAnimatorListenerAdapter
			{
				AnimationEnd = animation =>
				{
					_swipeListView.ResetScrolling();
					ResetCell();
				}
			};

			view.Animate()
				.TranslationX(0)
				.SetDuration(_animationTime)
				.SetListener(listener);
		}

		private int GetPointToMove(bool swap, bool swapRight, int position)
		{

			int moveTo = 0;
			if (Opened(position))
			{
				if (!swap)
				{
					if (OpenedRight(position))
					{
						moveTo = (int)(_viewWidth - RightOffset);
					}
					else
					{
						moveTo = (int)(-_viewWidth + LeftOffset);
					}
				}
			}
			else
			{
				if (swap)
				{
					moveTo = swapRight ? (int)(_viewWidth - RightOffset) : (int)(-_viewWidth + LeftOffset);
				}
			}

			return moveTo;
		}
	  
		/// <summary>
		///     Generates the dismiss animate.
		/// </summary>
		/// <param name="view">View.</param>
		/// <param name="swap">If set to <c>true</c> swap.</param>
		/// <param name="swapRight">If set to <c>true</c> swap right.</param>
		/// <param name="position">Position.</param>
		private void GenerateDismissAnimate(View view, bool swap, bool swapRight, int position)
		{
			int moveTo = GetPointToMove(swap, swapRight, position);
			int alpha = 1;
			if (swap)
			{
				++_dismissAnimationRefCount;
				alpha = 0;
			}

			var listener = new ObjectAnimatorListenerAdapter
			{
				AnimationEnd = animation =>
				{
					if (swap)
					{
						CloseOpenedItems();
						PerformDismiss(view, position, true);
					}
					ResetCell();
				}
			};

			view.Animate()
				.TranslationX(moveTo)
				.Alpha(alpha)
				.SetDuration(_animationTime)
				.SetListener(listener);
		}

		/// <summary>
		///     Generates the reveal animate.
		/// </summary>
		/// <param name="view">View.</param>
		/// <param name="swap">If set to <c>true</c> swap.</param>
		/// <param name="swapRight">If set to <c>true</c> swap right.</param>
		/// <param name="position">Position.</param>
		private void GenerateRevealAnimate(View view, bool swap, bool swapRight, int position)
		{
			int moveTo = GetPointToMove(swap, swapRight, position);
			var listener = new ObjectAnimatorListenerAdapter
			{
				AnimationEnd = animator =>
				{
					_swipeListView.ResetScrolling();
					if (swap)
					{
						bool aux = !Opened(position);
						_opened[position] = aux;
						if (aux)
						{
							_swipeListView.OnOpened(position, swapRight);
							_openedRight[position] = swapRight;
						}
						else
						{
							if (_backViews != null && BackView != null)
							{
								_backViews.ForEach(b =>
								{
									if (!Opened(b.Position))
									{
										b.BackView.Visibility = ViewStates.Gone;
										_backViews.Remove(b);
									}
								});
//							_backViews.Clear();
							}
							_swipeListView.OnClosed(position, OpenedRight(position));
						}
					}
					else
					{
						_swipeListView.OnClosed(position, OpenedRight(position));
					}
					ResetCell();
				},
				AnimationStart = animator =>
				{
					if (swap)
					{
						bool aux = !Opened(position);
						if (aux && _backViews != null && BackView != null)
						{
							BackView.Visibility = ViewStates.Visible;
							_backViews.Add(new BackViewHolder(BackView, position));
						}
					}
				}
			};

			view.Animate()
				.TranslationX(moveTo)
				.SetDuration(_animationTime)
				.SetListener(listener);
		}

		private void ResetCell()
		{
			if (_downPosition != AdapterView.InvalidPosition)
			{
				if (_swipeCurrentAction == (int) SwipeListView.SwipeAction.Choice)
				{
					BackView.Visibility = ViewStates.Visible;
				}
				FrontView.Clickable = Opened(_downPosition);
				FrontView.LongClickable = Opened(_downPosition);
				_backView = null;
				_downPosition = AdapterView.InvalidPosition;
			}
		}

		/// <summary>
		///     Sets the enabled.
		/// </summary>
		/// <param name="enabled">Enabled.</param>
		public void SetEnabled(bool enabled)
		{
			_paused = !enabled;
		}

		/// <summary>
		///     Closes the opened items.
		/// </summary>
		public void CloseOpenedItems()
		{
			if (_opened != null)
			{
				int start = _swipeListView.FirstVisiblePosition;
				int end = _swipeListView.LastVisiblePosition;
				for (int i = start; i <= end; i++)
				{
					if (Opened(i))
					{
						CloseAnimate(_swipeListView.GetChildAt(i - start).FindViewById(_swipeFrontView), i);
					}
				}
			}
		}

		private void SetActionsTo(int action)
		{
			_oldSwipeActionRight = SwipeActionRight;
			_oldSwipeActionLeft = SwipeActionLeft;
			SwipeActionRight = action;
			SwipeActionLeft = action;
		}

		public void ReturnOldActions()
		{
			SwipeActionRight = _oldSwipeActionRight;
			SwipeActionLeft = _oldSwipeActionLeft;
		}

		/// <summary>
		///     Move the specified deltaX.
		/// </summary>
		/// <param name="deltaX">Delta x.</param>
		public void Move(float deltaX)
		{
			_swipeListView.OnMove(_downPosition, deltaX);
			float posX = FrontView.GetX();
			if (Opened(_downPosition))
			{
				posX += OpenedRight(_downPosition) ? -_viewWidth + RightOffset : _viewWidth - LeftOffset;
			}
			if (posX > 0 && !_swipingRight)
			{
#if DEBUG
				Log.Debug("SwipeListView", "change to right");
#endif
				_swipingRight = !_swipingRight;
				_swipeCurrentAction = SwipeActionRight;
				BackView.Visibility = _swipeCurrentAction == (int) SwipeListView.SwipeAction.Choice ? ViewStates.Gone : ViewStates.Visible;
			}
			if (posX < 0 && _swipingRight)
			{
#if DEBUG
				Log.Debug("SwipeListView", "change to left");
#endif
				_swipingRight = !_swipingRight;
				_swipeCurrentAction = SwipeActionLeft;
				BackView.Visibility = _swipeCurrentAction == (int) SwipeListView.SwipeAction.Choice ? ViewStates.Gone : ViewStates.Visible;
			}
			if (_swipeCurrentAction == (int) SwipeListView.SwipeAction.Dismiss)
			{
				ParentView.TranslationX = deltaX;
				ParentView.Alpha = Math.Max(0f, Math.Min(1f, 1f - 2f*Math.Abs(deltaX)/_viewWidth));
			}
			else if (_swipeCurrentAction == (int) SwipeListView.SwipeAction.Choice)
			{
				if ((_swipingRight && deltaX > 0 && posX < DisplaceChoice)
					|| (!_swipingRight && deltaX < 0 && posX > -DisplaceChoice)
					|| (_swipingRight && deltaX < DisplaceChoice)
					|| (!_swipingRight && deltaX > -DisplaceChoice))
				{
					FrontView.TranslationX = deltaX;
				}
			}
			else
			{
				FrontView.TranslationX = deltaX;
			}
		}

		/// <summary>
		///     Performs the dismiss.
		/// </summary>
		/// <param name="dismissView">Dismiss view.</param>
		/// <param name="dismissPosition">Dismiss position.</param>
		/// <param name="doPendingDismiss">If set to <c>true</c> do pending dismiss.</param>
		protected void PerformDismiss(View dismissView, int dismissPosition, bool doPendingDismiss)
		{
			ViewGroup.LayoutParams lp = dismissView.LayoutParameters;
			EnableDisableViewGroup((ViewGroup) dismissView, false);
			int originalHeight = dismissView.Height;

			var animator = (ValueAnimator) ValueAnimator.OfInt(originalHeight, 1).SetDuration(_animationTime);

			if (doPendingDismiss)
			{
				var pendingDismissListener = new ObjectAnimatorListenerAdapter
				{
					AnimationEnd = valueAnimator =>
					{
						--_dismissAnimationRefCount;
						if (_dismissAnimationRefCount == 0)
						{
							RemovePendingDismisses(originalHeight);
						}
					}
				};

				animator.AddListener(pendingDismissListener);
			}

			var listener = new ObjectAnimatorListenerAdapter
			{
				AnimationEnd = valueAnimator =>
					EnableDisableViewGroup((ViewGroup) dismissView, true)
			};

			animator.AddListener(listener);

			var updateListener = new ObjectAnimatorUpdateListener
			{
				AnimationUpdate = valueAnimator =>
				{
					lp.Height = (int) valueAnimator.AnimatedValue;
					dismissView.LayoutParameters = lp;
				}
			};


			_pendingDismisses.Add(new PendingDismissData(dismissPosition, dismissView));
			animator.Start();
		}

		public void ResetPendingDismisses()
		{
			_pendingDismisses.Clear();
		}

		public async void HandlerPendingDismisses(int originalHeight)
		{
			await Task.Delay(Convert.ToInt32(_animationTime) + 100);
			await Task.Run(() => RemovePendingDismisses(originalHeight));
		}

		private void RemovePendingDismisses(int originalHeight)
		{
			// No active animations, process all pending dismisses.
			// Sort by descending position
			_pendingDismisses.Sort();

			var dismissPositions = new int[_pendingDismisses.Count];
			for (int i = _pendingDismisses.Count - 1; i >= 0; i--)
			{
				dismissPositions[i] = _pendingDismisses[i].Position;
			}
			_swipeListView.OnDismiss(dismissPositions);

			ViewGroup.LayoutParams lp;
			foreach (PendingDismissData pendingDismiss in _pendingDismisses)
			{
				// Reset view presentation
				if (pendingDismiss.View != null)
				{
					pendingDismiss.View.Alpha = 1f;
					pendingDismiss.View.TranslationX = 0;
					lp = pendingDismiss.View.LayoutParameters;
					lp.Height = originalHeight;
					pendingDismiss.View.LayoutParameters = lp;
				}
			}

			ResetPendingDismisses();
		}

		public static void EnableDisableViewGroup(ViewGroup viewGroup, bool enabled)
		{
			int childCount = viewGroup.ChildCount;
			for (int i = 0; i < childCount; i++)
			{
				View view = viewGroup.GetChildAt(i);
				view.Enabled = enabled;
				if (view is ViewGroup)
				{
					EnableDisableViewGroup((ViewGroup) view, enabled);
				}
			}
		}

		#region IOnTouchListener implementation

		public bool OnTouch(View v, MotionEvent e)
		{
			float velocityX, velocityY, deltaX;
			int localSwipeMode = SwipeMode;

			if (!IsSwipeEnabled)
			{
				return false;
			}

			if (_viewWidth < 2)
			{
				_viewWidth = _swipeListView.Width;
			}

			switch (MotionEventCompat.GetActionMasked(e))
			{
				case (int) MotionEventActions.Down:
					if (_paused && _downPosition != AdapterView.InvalidPosition)
					{
						return false;
					}
					_swipeCurrentAction = (int) SwipeListView.SwipeAction.None;

					int childCount = _swipeListView.ChildCount;
					var listViewCoords = new int[2];
					_swipeListView.GetLocationOnScreen(listViewCoords);
					int x = (int) e.RawX - listViewCoords[0];
					int y = (int) e.RawY - listViewCoords[1];
					View child;
					for (int i = 0; i < childCount; i++)
					{
						child = _swipeListView.GetChildAt(i);
						child.GetHitRect(_rect);

						_childPosition = _swipeListView.GetPositionForView(child);

						// dont allow swiping if this is on the header or footer or IGNORE_ITEM_VIEW_TYPE or enabled is false on the adapter
						bool allowSwipe = _swipeListView.Adapter.IsEnabled(_childPosition) &&
										  _swipeListView.Adapter.GetItemViewType(_childPosition) >= 0;

						if (allowSwipe && _rect.Contains(x, y))
						{
							ParentView = child;
							View viewHolder = child.FindViewById(_swipeFrontView);
							if (viewHolder != null)
							{
								FrontView = viewHolder;
								FrontView.Clickable = !Opened(_downPosition);
								FrontView.LongClickable = !Opened(_downPosition);
							}

							_downX = e.RawX;
							_downPosition = _childPosition - _swipeListView.HeaderViewsCount;

							_velocityTracker = VelocityTracker.Obtain();
							_velocityTracker.AddMovement(e);
							if (_swipeBackView > 0)
							{
								BackView = child.FindViewById(_swipeBackView);
							}
							break;
						}
					}
					v.OnTouchEvent(e);
					return true;

				case (int) MotionEventActions.Up:
					if (_velocityTracker == null || !_swiping || _downPosition == AdapterView.InvalidPosition)
					{
						break;
					}

					deltaX = e.RawX - _downX;
					_velocityTracker.AddMovement(e);
					_velocityTracker.ComputeCurrentVelocity(1000);
					velocityX = Math.Abs(_velocityTracker.XVelocity);
					if (Opened(_downPosition))
					{
						if (localSwipeMode == (int) SwipeListView.SwipeMode.Left && _velocityTracker.XVelocity > 0)
						{
							velocityX = 0;
						}
						if (localSwipeMode == (int) SwipeListView.SwipeMode.Right && _velocityTracker.XVelocity < 0)
						{
							velocityX = 0;
						}
					}
					velocityY = Math.Abs(_velocityTracker.YVelocity);
					bool swap = false;
					bool swapRight = false;
					if (_minFlingVelocity <= velocityX && velocityX <= _maxFlingVelocity && velocityY*2 < velocityX)
					{
						swapRight = _velocityTracker.XVelocity > 0;
#if DEBUG
						Log.Debug("SwipeListView", "swapRight: " + swapRight + " - swipingRight: " + _swipingRight);
#endif
						if (swapRight != _swipingRight && SwipeActionLeft != SwipeActionRight)
						{
							swap = false;
						}
						else if (Opened(_downPosition) && OpenedRight(_downPosition) && swapRight)
						{
							swap = false;
						}
						else if (Opened(_downPosition) && !OpenedRight(_downPosition) && !swapRight)
						{
							swap = false;
						}
						else
						{
							swap = true;
						}
					}
					else if (Math.Abs(deltaX) > _viewWidth / SwipeOffset)
					{
						swap = true;
						swapRight = deltaX > 0;
					}

					GenerateAnimate(FrontView, swap, swapRight, _downPosition);

					if (_swipeCurrentAction == (int) SwipeListView.SwipeAction.Choice)
					{
						SwapChoiceState(_downPosition);
					}

					_velocityTracker.Recycle();
					_velocityTracker = null;
					_downX = 0;
					_swiping = false;

					break;

				case (int) MotionEventActions.Move:
					if (_velocityTracker == null || _paused || _downPosition == AdapterView.InvalidPosition)
					{
						break;
					}

					_velocityTracker.AddMovement(e);
					_velocityTracker.ComputeCurrentVelocity(1000);

					velocityX = Math.Abs(_velocityTracker.XVelocity);
					velocityY = Math.Abs(_velocityTracker.YVelocity);

					deltaX = e.RawX - _downX;
					float deltaMode = Math.Abs(deltaX);

					int changeSwipeMode = _swipeListView.ChangeSwipeMode(_downPosition);
					if (changeSwipeMode >= 0)
					{
						localSwipeMode = changeSwipeMode;
					}

					if (localSwipeMode == (int) SwipeListView.SwipeMode.None)
					{
						deltaMode = 0;
					}
					else if (localSwipeMode != (int) SwipeListView.SwipeMode.Both)
					{
						if (Opened(_downPosition))
						{
							if (localSwipeMode == (int) SwipeListView.SwipeMode.Left && deltaX < 0)
							{
								deltaMode = 0;
							}
							else if (localSwipeMode == (int) SwipeListView.SwipeMode.Right && deltaX > 0)
							{
								deltaMode = 0;
							}
						}
						else
						{
							if (localSwipeMode == (int) SwipeListView.SwipeMode.Left && deltaX > 0)
							{
								deltaMode = 0;
							}
							else if (localSwipeMode == (int) SwipeListView.SwipeMode.Right && deltaX < 0)
							{
								deltaMode = 0;
							}
						}
					}

					if (deltaMode > _slop && _swipeCurrentAction == (int) SwipeListView.SwipeAction.None &&
						velocityY < velocityX)
					{
						_swiping = true;
						_swipingRight = (deltaX > 0);
#if DEBUG
						Log.Debug("SwipeListView", "deltaX: " + deltaX + " - swipingRight: " + _swipingRight);
#endif
						if (Opened(_downPosition))
						{
							_swipeListView.OnStartClose(_downPosition, _swipingRight);
							_swipeCurrentAction = (int) SwipeListView.SwipeAction.Reveal;
						}
						else
						{
							if (_swipingRight && SwipeActionRight == (int) SwipeListView.SwipeAction.Dismiss)
							{
								_swipeCurrentAction = (int) SwipeListView.SwipeAction.Dismiss;
							}
							else if (!_swipingRight && SwipeActionLeft == (int) SwipeListView.SwipeAction.Dismiss)
							{
								_swipeCurrentAction = (int) SwipeListView.SwipeAction.Dismiss;
							}
							else if (_swipingRight && SwipeActionRight == (int) SwipeListView.SwipeAction.Choice)
							{
								_swipeCurrentAction = (int) SwipeListView.SwipeAction.Choice;
							}
							else if (!_swipingRight && SwipeActionLeft == (int) SwipeListView.SwipeAction.Choice)
							{
								_swipeCurrentAction = (int) SwipeListView.SwipeAction.Choice;
							}
							else
							{
								_swipeCurrentAction = (int) SwipeListView.SwipeAction.Reveal;
							}

							_swipeListView.OnStartOpen(_downPosition, _swipeCurrentAction, _swipingRight);
						}

						_swipeListView.RequestDisallowInterceptTouchEvent(true);
						MotionEvent cancelEvent = MotionEvent.Obtain(e);
						cancelEvent.Action =
							(MotionEventActions)
								((int) MotionEventActions.Cancel |
								 (MotionEventCompat.GetActionIndex(e) << MotionEventCompat.ActionPointerIndexShift));
						_swipeListView.OnTouchEvent(cancelEvent);
						if (_swipeCurrentAction == (int) SwipeListView.SwipeAction.Choice)
						{
							BackView.Visibility = ViewStates.Gone;
						}
					}

					if (_swiping && _downPosition != AdapterView.InvalidPosition)
					{
						if (Opened(_downPosition))
						{
							deltaX += OpenedRight(_downPosition) ? _viewWidth - RightOffset : -_viewWidth + LeftOffset;
						}
						Move(deltaX);
						return true;
					}
					break;
			}
			return false;
		}

		#endregion

		#region "AbsListView.IOnScrollListener"

		public void OnScroll(AbsListView view, int firstVisibleItem, int visibleItemCount, int totalItemCount)
		{
			if (_isFirstItem)
			{
				bool onSecondItemList = firstVisibleItem == 1;
				if (onSecondItemList)
				{
					_isFirstItem = false;
				}
			}
			else
			{
				bool onFirstItemList = firstVisibleItem == 0;
				if (onFirstItemList)
				{
					_isFirstItem = true;
					_swipeListView.OnFirstListItem();
				}
			}

			if (_isLastItem)
			{
				bool onBeforeLastItemList = firstVisibleItem + visibleItemCount == totalItemCount - 1;
				if (onBeforeLastItemList)
				{
					_isLastItem = false;
				}
			}
			else
			{
				bool onLastItemList = firstVisibleItem + visibleItemCount >= totalItemCount;
				if (onLastItemList)
				{
					_isLastItem = true;
					_swipeListView.OnLastListItem();
				}
			}
		}

		public async void OnScrollStateChanged(AbsListView view, ScrollState scrollState)
		{
			SetEnabled(scrollState != ScrollState.TouchScroll);
			if (SwipeClosesAllItemsWhenListMoves && scrollState == ScrollState.TouchScroll)
			{
				CloseOpenedItems();
			}
			if (scrollState == ScrollState.TouchScroll)
			{
				IsListViewMoving = true;
				SetEnabled(false);
			}

			if (scrollState != ScrollState.Fling && scrollState != ScrollState.TouchScroll)
			{
				IsListViewMoving = false;
				_downPosition = AdapterView.InvalidPosition;
				_swipeListView.ResetScrolling();

				await Task.Delay(500);
				await Task.Run(() => SetEnabled(true));
			}
		}

		#endregion
	}

	public class PendingDismissData : IComparable
	{
		public PendingDismissData(int position, View view)
		{
			Position = position;
			View = view;
		}

		public int Position { get; set; }
		public View View { get; set; }

		public int CompareTo(object obj)
		{
			var data = (PendingDismissData) obj;
			// Sort by descending position
			return data.Position - Position;
		}
	}

	public class ObjectAnimatorListenerAdapter : AnimatorListenerAdapter
	{
		public ObjectAnimatorListenerAdapter()
		{
			AnimationEnd = animator => { };
		}

		public Action<Animator> AnimationEnd { get; set; }

		public Action<Animator> AnimationStart { get; set; }

		public override void OnAnimationEnd(Animator animator)
		{
			AnimationEnd(animator);
		}

		public override void OnAnimationStart(Animator animator)
		{
			if (AnimationStart == null) return;
			AnimationStart(animator);
		}
	}

	public class ObjectAnimatorUpdateListener : Object, ValueAnimator.IAnimatorUpdateListener
	{
		public ObjectAnimatorUpdateListener()
		{
			AnimationUpdate = valueAnimator => { };
		}

		public Action<ValueAnimator> AnimationUpdate { get; set; }

		#region IAnimatorUpdateListener implementation

		public void OnAnimationUpdate(ValueAnimator valueAnimator)
		{
			AnimationUpdate(valueAnimator);
		}

		#endregion

	    public void Dispose()
	    {
	       
	    }

	    public IntPtr Handle { get; private set; }
	}

	public class BackViewHolder
	{
		public BackViewHolder(View backView, int position)
		{
			BackView = backView;
			Position = position;
		}

		public View BackView { get; set; }
		public int Position { get; set; }
	}
}

