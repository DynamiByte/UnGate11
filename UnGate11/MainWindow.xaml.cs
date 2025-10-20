using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Media;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace UnGate11
{
    public partial class MainWindow : Window, IDisposable
    {
        // Constants
        private const int INVALID_CLICK_THRESHOLD = 3;
        private const int BUTTON_CONTENT_DELAY_MS = 2000;
        private const int TASK_COMPLETION_DELAY_MS = 500;
        private const double GIF_FRAME_DELAY_MS = 24;
        

        // Fields
        // GIF animation
        private BitmapDecoder _gifDecoder;
        private int _gifFrameIndex;
        private DispatcherTimer _gifTimer;
        private DispatcherTimer _delayTimer;

        // State management
        private string _currentPatchState = "idkbruh";
        private bool _interactionEnabled = true;
        private int _invalidClickCount = 0;
        private bool _disposed = false;

        // System helper
        private readonly MainToolsHelper _systemHelper;
        private readonly AdvancedToolsHelper _advancedToolsHelper;

        // UI elements
        private TextBlock _progressTextBlock;
        private bool _inAdvancedMode = false;
        private List<string> _availableEditions = new List<string>();

        // Cached storyboards
        private Storyboard _fadeLogoIn, _fadeLogoOut;
        private Storyboard _fadeLoadingIn, _fadeLoadingOut;
        private Storyboard _fadePatchButtonIn, _fadePatchButtonOut;
        private Storyboard _fadeRefreshButtonIn, _fadeRefreshButtonOut;
        private Storyboard _fadeAdvancedButtonIn, _fadeAdvancedButtonOut;
        

        // Constructor and Initialization
        public MainWindow()
        {
            InitializeComponent();
            InitializeProgressTextBlock();
            CacheStoryboards();

            // Initialize system helper directly in constructor
            _systemHelper = new MainToolsHelper();
            _systemHelper.PatchStatusChecked += SystemHelper_PatchStatusChecked;
            _systemHelper.PatchActionCompleted += SystemHelper_PatchActionCompleted;
            _systemHelper.WindowsUpdateRefreshed += SystemHelper_WindowsUpdateRefreshed;
            _systemHelper.ProgressReported += SystemHelper_ProgressReported;

            // Initialize advanced tools helper
            _advancedToolsHelper = new AdvancedToolsHelper();
            _advancedToolsHelper.ProgressReported += AdvancedToolsHelper_ProgressReported;
            _advancedToolsHelper.OutputReceived += AdvancedToolsHelper_OutputReceived;
            _advancedToolsHelper.EditionsFound += AdvancedToolsHelper_EditionsFound;

            InitializeVersionLabel();

            StateChanged += MainWindow_StateChanged;
            PatchButton.Content = "Checking Patch State...";

            // Check patch status using helper
            Task.Run(async () => await _systemHelper.CheckPatchStatus());

            // Start loading animation
            StartTask(null);
        }

        private void InitializeProgressTextBlock()
        {
            _progressTextBlock = new TextBlock
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Foreground = Brushes.White,
                FontSize = 14,
                Opacity = 0,
                Visibility = Visibility.Collapsed,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 300,
                Margin = new Thickness(0, -50, 0, 0)
            };

            if (Content is Grid mainGrid)
            {
                mainGrid.Children.Add(_progressTextBlock);
            }
        }

        private void CacheStoryboards()
        {
            _fadeLogoIn = (Storyboard)FindResource("FadeLogoIn");
            _fadeLogoOut = (Storyboard)FindResource("FadeLogoOut");
            _fadeLoadingIn = (Storyboard)FindResource("FadeLoadingIn");
            _fadeLoadingOut = (Storyboard)FindResource("FadeLoadingOut");
            _fadePatchButtonIn = (Storyboard)FindResource("FadePatchButtonIn");
            _fadePatchButtonOut = (Storyboard)FindResource("FadePatchButtonOut");
            _fadeRefreshButtonIn = (Storyboard)FindResource("FadeRefreshButtonIn");
            _fadeRefreshButtonOut = (Storyboard)FindResource("FadeRefreshButtonOut");
            _fadeAdvancedButtonIn = (Storyboard)FindResource("FadeAdvancedButtonIn");
            _fadeAdvancedButtonOut = (Storyboard)FindResource("FadeAdvancedButtonOut");
        }

        private void InitializeVersionLabel()
        {
            var version = GetVersion();
            VersionLabel.Content = $"v{version.Major}.{version.Minor}.{version.Build}";
        }
        

        // System Helper Event Handlers
        private void SystemHelper_PatchStatusChecked(object sender, MainToolsHelper.StatusEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                switch (e.StatusCode)
                {
                    case "C1":
                        PatchButton.Content = "Patch";
                        _currentPatchState = "unpatched";
                        EndTask();
                        break;
                    case "C0":
                        PatchButton.Content = "Unpatch";
                        _currentPatchState = "patched";
                        EndTask();
                        break;
                    default:
                        PatchButton.Content = "Patch";
                        EndTask();
                        break;
                }
            });
        }

        private void SystemHelper_PatchActionCompleted(object sender, MainToolsHelper.StatusEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                switch (e.StatusCode)
                {
                    case "P0":
                        _currentPatchState = "patched";
                        PatchButton.Content = "Patch Applied";
                        DelayedContent(PatchButton, "Unpatch", BUTTON_CONTENT_DELAY_MS);
                        EndTask();
                        break;
                    case "P1":
                        _currentPatchState = "unpatched";
                        PatchButton.Content = "Patch Removed";
                        DelayedContent(PatchButton, "Patch", BUTTON_CONTENT_DELAY_MS);
                        EndTask();
                        break;
                    default:
                        EndTask();
                        break;
                }
            });
        }

        private void SystemHelper_WindowsUpdateRefreshed(object sender, MainToolsHelper.StatusEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                if (e.StatusCode == "WUR")
                {
                    UpdateRefreshButton.Content = "Refreshed";
                    DelayedContent(UpdateRefreshButton, "Refresh Windows Update", BUTTON_CONTENT_DELAY_MS);
                    EndTask();
                }
            });
        }

        private void SystemHelper_ProgressReported(object sender, MainToolsHelper.ProgressEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                _progressTextBlock.Text = e.Message;
            });
        }
        

        // Task Management
        private void SetInteractionEnabled(bool state)
        {
            _interactionEnabled = state;

            // Keep controls enabled so they still receive mouse events when "disabled"
            if (PatchButton != null) PatchButton.IsEnabled = true;
            if (UpdateRefreshButton != null) UpdateRefreshButton.IsEnabled = true;
            if (AdvancedToolsButton != null) AdvancedToolsButton.IsEnabled = true;

            // Visual disabled hint
            double visualOpacity = state ? 1.0 : 0.6;
            if (PatchButton != null) PatchButton.Opacity = visualOpacity;
            if (UpdateRefreshButton != null) UpdateRefreshButton.Opacity = visualOpacity;
            if (AdvancedToolsButton != null) AdvancedToolsButton.Opacity = visualOpacity;

            // Optionally change cursor to indicate disabled state
            if (PatchButton != null) PatchButton.Cursor = state ? Cursors.Hand : Cursors.Arrow;
            if (UpdateRefreshButton != null) UpdateRefreshButton.Cursor = state ? Cursors.Hand : Cursors.Arrow;
            if (AdvancedToolsButton != null) AdvancedToolsButton.Cursor = state ? Cursors.Hand : Cursors.Arrow;
        }

        private void EndTask()
        {
            if (_delayTimer == null)
            {
                _delayTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(TASK_COMPLETION_DELAY_MS) };
            }

            _delayTimer.Tick += OnEndTaskDelay;
            _delayTimer.Start();
        }

        private void OnEndTaskDelay(object sender, EventArgs e)
        {
            _delayTimer.Stop();
            _delayTimer.Tick -= OnEndTaskDelay;

            _fadeLogoIn.Begin();
            _fadeLoadingOut.Begin();
            _fadePatchButtonIn.Begin();
            _fadeRefreshButtonIn.Begin();
            _fadeAdvancedButtonIn.Begin();

            _progressTextBlock.Visibility = Visibility.Collapsed;
            _progressTextBlock.Opacity = 0;

            StopLoadingGIF();
            SetInteractionEnabled(true);
            _invalidClickCount = 0;
        }

        private void StartTask(string type)
        {
            if (type == "patch")
            {
                PatchButton.Content = _currentPatchState == "unpatched" ? "Patching" : "Unpatching";
                _progressTextBlock.Visibility = Visibility.Visible;
                _progressTextBlock.Opacity = 1;
            }
            else if (type == "refresh")
            {
                UpdateRefreshButton.Content = "Refreshing";
                _progressTextBlock.Visibility = Visibility.Visible;
                _progressTextBlock.Opacity = 1;
                _progressTextBlock.Text = "Starting Windows Update refresh...";
            }
            else
            {
                _progressTextBlock.Visibility = Visibility.Collapsed;
                _progressTextBlock.Opacity = 0;
            }

            SetInteractionEnabled(false);
            _fadeLogoOut.Begin();
            _fadePatchButtonOut.Begin();
            _fadeRefreshButtonOut.Begin();
            _fadeAdvancedButtonOut.Begin();
            _fadeLoadingIn.Begin();
            StartLoadingGIF();
        }

        private async void DelayedContent(ContentControl control, object content, int delayMs)
        {
            await Task.Delay(delayMs).ConfigureAwait(false);
            await Dispatcher.InvokeAsync(() => control.Content = content);
        }
        

        // GIF Animation
        private void LoadGifDecoder()
        {
            if (_gifDecoder != null) return;

            string tempGIFPath = Path.Combine(Path.GetTempPath(), "load.gif");
            if (!File.Exists(tempGIFPath))
                ExtractResourceToFile("load.gif", tempGIFPath);

            using (var fileStream = new FileStream(tempGIFPath, FileMode.Open, FileAccess.Read))
            {
                _gifDecoder = BitmapDecoder.Create(fileStream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            }
        }

        private void StartLoadingGIF()
        {
            LoadGifDecoder();

            _gifFrameIndex = 0;
            LoadingGIF.Source = _gifDecoder.Frames[_gifFrameIndex];
            LoadingGIF.Visibility = Visibility.Visible;
            LoadingGIF.Opacity = 1;

            if (_gifTimer == null)
            {
                _gifTimer = new DispatcherTimer(DispatcherPriority.Render)
                {
                    Interval = TimeSpan.FromMilliseconds(GIF_FRAME_DELAY_MS)
                };
                _gifTimer.Tick += GifTimer_Tick;
            }
            _gifTimer.Start();
        }

        private void GifTimer_Tick(object sender, EventArgs e)
        {
            _gifFrameIndex = (_gifFrameIndex + 1) % _gifDecoder.Frames.Count;
            LoadingGIF.Source = _gifDecoder.Frames[_gifFrameIndex];
        }

        private void StopLoadingGIF()
        {
            _gifTimer?.Stop();
            LoadingGIF.Visibility = Visibility.Collapsed;
            LoadingGIF.Opacity = 0;
        }


        // Event Handlers
        private async void PatchButton_Left(object sender, MouseButtonEventArgs e)
        {
            if (!_interactionEnabled)
            {
                HandleInvalidButtonClick(PatchButton);
                return;
            }

            StartTask("patch");

            if (_currentPatchState == "unpatched")
            {
                await _systemHelper.ApplyPatch();
            }
            else if (_currentPatchState == "patched")
            {
                await _systemHelper.RemovePatch();
            }
        }

        private void PatchButton_Right(object sender, MouseButtonEventArgs e)
        {
            // Reserved for future functionality
        }

        // Use _interactionEnabled instead of control.IsEnabled so clicks still reach the handler
        private async void UpdateRefreshButton_Left(object sender, MouseButtonEventArgs e)
        {
            if (!_interactionEnabled)
            {
                HandleInvalidButtonClick(UpdateRefreshButton);
                return;
            }

            StartTask("refresh");

            await _systemHelper.RefreshWindowsUpdate();
        }

        private void UpdateRefreshButton_Right(object sender, MouseButtonEventArgs e)
        {
            // Reserved for future functionality
        }

        private void AdvancedToolsButton_Click(object sender, MouseButtonEventArgs e)
        {
            if (!_interactionEnabled)
            {
                HandleInvalidButtonClick(AdvancedToolsButton);
                return;
            }

            // Transition to advanced tools menu
            TransitionToAdvancedTools();
        }

        private void TransitionToAdvancedTools()
        {
            _inAdvancedMode = true;
            SetInteractionEnabled(false);

            // Animate logo up and scale down
            var logoMoveUp = new DoubleAnimation
            {
                To = -35,
                Duration = TimeSpan.FromMilliseconds(400),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };

            var logoScaleDown = new DoubleAnimation
            {
                To = 0.6,
                Duration = TimeSpan.FromMilliseconds(400),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };

            LogoTranslate.BeginAnimation(TranslateTransform.YProperty, logoMoveUp);
            LogoScale.BeginAnimation(ScaleTransform.ScaleXProperty, logoScaleDown);
            LogoScale.BeginAnimation(ScaleTransform.ScaleYProperty, logoScaleDown);

            // Fade out main button panel
            var fadeOut = new DoubleAnimation
            {
                To = 0,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            fadeOut.Completed += (s, e) =>
            {
                MainButtonPanel.Visibility = Visibility.Collapsed;

                // Show advanced subtitle and menu
                AdvancedSubtitle.Visibility = Visibility.Visible;
                AdvancedMenuPanel.Visibility = Visibility.Visible;

                var fadeIn = new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = TimeSpan.FromMilliseconds(400),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };

                AdvancedSubtitle.BeginAnimation(OpacityProperty, fadeIn);
                AdvancedMenuPanel.BeginAnimation(OpacityProperty, fadeIn);
                SetInteractionEnabled(true);
            };

            MainButtonPanel.BeginAnimation(OpacityProperty, fadeOut);
        }

        private void TransitionToMainMenu()
        {
            _inAdvancedMode = false;
            SetInteractionEnabled(false);

            // Animate logo back down and scale up
            var logoMoveDown = new DoubleAnimation
            {
                To = 0,
                Duration = TimeSpan.FromMilliseconds(400),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };

            var logoScaleUp = new DoubleAnimation
            {
                To = 1,
                Duration = TimeSpan.FromMilliseconds(400),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };

            LogoTranslate.BeginAnimation(TranslateTransform.YProperty, logoMoveDown);
            LogoScale.BeginAnimation(ScaleTransform.ScaleXProperty, logoScaleUp);
            LogoScale.BeginAnimation(ScaleTransform.ScaleYProperty, logoScaleUp);

            // Fade out advanced panels
            var fadeOut = new DoubleAnimation
            {
                To = 0,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            fadeOut.Completed += (s, e) =>
            {
                AdvancedSubtitle.Visibility = Visibility.Collapsed;
                AdvancedMenuPanel.Visibility = Visibility.Collapsed;
                AdvancedOutputScrollViewer.Visibility = Visibility.Collapsed;
                EditionSelectionPanel.Visibility = Visibility.Collapsed;
                BackToMenuFromOutputButton.Visibility = Visibility.Collapsed;
                AdvancedOutputTextBlock.Text = "";

                // Show main button panel
                MainButtonPanel.Visibility = Visibility.Visible;

                var fadeIn = new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = TimeSpan.FromMilliseconds(400),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };

                MainButtonPanel.BeginAnimation(OpacityProperty, fadeIn);
                SetInteractionEnabled(true);
            };

            AdvancedSubtitle.BeginAnimation(OpacityProperty, fadeOut);
            AdvancedMenuPanel.BeginAnimation(OpacityProperty, fadeOut);
        }

        // Advanced Tools Event Handlers
        private void BackToMainButton_Click(object sender, MouseButtonEventArgs e)
        {
            TransitionToMainMenu();
        }

        private void BackToMenuFromOutput_Click(object sender, MouseButtonEventArgs e)
        {
            // This handles going back from output screen OR edition selection
            // Fade out output and button, show advanced menu
            var fadeOut = new DoubleAnimation
            {
                To = 0,
                Duration = TimeSpan.FromMilliseconds(300)
            };

            fadeOut.Completed += (s, ev) =>
            {
                AdvancedOutputScrollViewer.Visibility = Visibility.Collapsed;
                BackToMenuFromOutputButton.Visibility = Visibility.Collapsed;
                BackToMenuFromOutputButton.Opacity = 0;
                EditionSelectionPanel.Visibility = Visibility.Collapsed;
                EditionButtonsPanel.Children.Clear();
                _availableEditions.Clear();
                AdvancedOutputTextBlock.Text = "";
                AdvancedMenuPanel.Visibility = Visibility.Visible;

                var fadeIn = new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = TimeSpan.FromMilliseconds(400)
                };
                AdvancedMenuPanel.BeginAnimation(OpacityProperty, fadeIn);
            };

            // Fade out whichever is visible
            if (AdvancedOutputScrollViewer.Visibility == Visibility.Visible)
            {
                AdvancedOutputScrollViewer.BeginAnimation(OpacityProperty, fadeOut);
            }
            if (EditionSelectionPanel.Visibility == Visibility.Visible)
            {
                EditionSelectionPanel.BeginAnimation(OpacityProperty, fadeOut);
            }
            if (BackToMenuFromOutputButton.Visibility == Visibility.Visible)
            {
                BackToMenuFromOutputButton.BeginAnimation(OpacityProperty, fadeOut);
            }
        }

        private void ShowBackToMenuButton()
        {
            BackToMenuFromOutputButton.Visibility = Visibility.Visible;

            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(400),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            BackToMenuFromOutputButton.BeginAnimation(OpacityProperty, fadeIn);
        }

        private async void EditionChangeButtonAdv_Click(object sender, MouseButtonEventArgs e)
        {
            var result = MessageBox.Show(
                "This tool will check your current Windows edition and show available target editions.\n\n" +
                "WARNING: Changing Windows edition can cause system instability if not done correctly.\n\n" +
                "Continue?",
                "Change Windows Edition",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            // Hide menu, show output
            var fadeOut = new DoubleAnimation
            {
                To = 0,
                Duration = TimeSpan.FromMilliseconds(300)
            };

            fadeOut.Completed += async (s, ev) =>
            {
                AdvancedMenuPanel.Visibility = Visibility.Collapsed;
                AdvancedOutputScrollViewer.Visibility = Visibility.Visible;
                LoadingGIF.Visibility = Visibility.Visible;
                StartLoadingGIF();

                var fadeIn = new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = TimeSpan.FromMilliseconds(300)
                };
                AdvancedOutputScrollViewer.BeginAnimation(OpacityProperty, fadeIn);

                await Task.Run(async () => await _advancedToolsHelper.ChangeWindowsEdition());

                StopLoadingGIF();
                LoadingGIF.Visibility = Visibility.Collapsed;

                // Show back to menu button
                ShowBackToMenuButton();
            };

            AdvancedMenuPanel.BeginAnimation(OpacityProperty, fadeOut);
        }

        private async void ActivationStatusButtonAdv_Click(object sender, MouseButtonEventArgs e)
        {
            // Hide menu, show output
            var fadeOut = new DoubleAnimation
            {
                To = 0,
                Duration = TimeSpan.FromMilliseconds(300)
            };

            fadeOut.Completed += async (s, ev) =>
            {
                AdvancedMenuPanel.Visibility = Visibility.Collapsed;
                AdvancedOutputScrollViewer.Visibility = Visibility.Visible;
                LoadingGIF.Visibility = Visibility.Visible;
                StartLoadingGIF();

                var fadeIn = new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = TimeSpan.FromMilliseconds(300)
                };
                AdvancedOutputScrollViewer.BeginAnimation(OpacityProperty, fadeIn);

                await Task.Run(async () => await _advancedToolsHelper.CheckActivationStatus());

                StopLoadingGIF();
                LoadingGIF.Visibility = Visibility.Collapsed;

                // Show back to menu button
                ShowBackToMenuButton();
            };

            AdvancedMenuPanel.BeginAnimation(OpacityProperty, fadeOut);
        }

        private async void HwidActivationButtonAdv_Click(object sender, MouseButtonEventArgs e)
        {
            // Hide menu, show output
            var fadeOut = new DoubleAnimation
            {
                To = 0,
                Duration = TimeSpan.FromMilliseconds(300)
            };

            fadeOut.Completed += async (s, ev) =>
            {
                AdvancedMenuPanel.Visibility = Visibility.Collapsed;
                AdvancedOutputScrollViewer.Visibility = Visibility.Visible;
                LoadingGIF.Visibility = Visibility.Visible;
                StartLoadingGIF();

                var fadeIn = new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = TimeSpan.FromMilliseconds(300)
                };
                AdvancedOutputScrollViewer.BeginAnimation(OpacityProperty, fadeIn);

                await Task.Run(async () => await _advancedToolsHelper.HwidActivate());

                StopLoadingGIF();
                LoadingGIF.Visibility = Visibility.Collapsed;

                // Show back to menu button
                ShowBackToMenuButton();
            };

            AdvancedMenuPanel.BeginAnimation(OpacityProperty, fadeOut);
        }

        private void AdvancedToolsHelper_ProgressReported(object sender, string message)
        {
            Dispatcher.Invoke(() =>
            {
                // Progress messages are also sent as output
            });
        }

        private void AdvancedToolsHelper_OutputReceived(object sender, string output)
        {
            Dispatcher.Invoke(() =>
            {
                AdvancedOutputTextBlock.Text += output + Environment.NewLine;
                AdvancedOutputScrollViewer.ScrollToEnd();
            });
        }

        private void AdvancedToolsHelper_EditionsFound(object sender, List<string> editions)
        {
            Dispatcher.Invoke(() =>
            {
                _availableEditions = editions;
                ShowEditionSelectionInMainWindow();
            });
        }

        private void ShowEditionSelectionInMainWindow()
        {
            // Clear existing buttons
            EditionButtonsPanel.Children.Clear();

            // Create a button for each edition
            foreach (var edition in _availableEditions)
            {
                var button = CreateEditionButtonForMain(edition);
                button.Opacity = 0;
                EditionButtonsPanel.Children.Add(button);
            }

            // Fade out output, then fade in edition selection
            var fadeOutOutput = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            fadeOutOutput.Completed += (s, e) =>
            {
                AdvancedOutputScrollViewer.Visibility = Visibility.Collapsed;
                EditionSelectionPanel.Visibility = Visibility.Visible;
                LoadingGIF.Visibility = Visibility.Collapsed;
                StopLoadingGIF();
                
                // Show the back to menu button for edition selection
                ShowBackToMenuButton();

                var fadeInPanel = new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = TimeSpan.FromMilliseconds(400),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };

                EditionSelectionPanel.BeginAnimation(UIElement.OpacityProperty, fadeInPanel);

                // Fade in each button with stagger
                int delay = 0;
                foreach (var child in EditionButtonsPanel.Children)
                {
                    if (child is Label btn)
                    {
                        var fadeInButton = new DoubleAnimation
                        {
                            From = 0,
                            To = 1,
                            Duration = TimeSpan.FromMilliseconds(300),
                            BeginTime = TimeSpan.FromMilliseconds(delay),
                            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                        };
                        btn.BeginAnimation(UIElement.OpacityProperty, fadeInButton);
                        delay += 80;
                    }
                }
            };

            AdvancedOutputScrollViewer.BeginAnimation(UIElement.OpacityProperty, fadeOutOutput);
        }

        private Label CreateEditionButtonForMain(string edition)
        {
            var button = new Label
            {
                Content = edition,
                FontFamily = new FontFamily("Bahnschrift"),
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                Background = new SolidColorBrush(Color.FromRgb(33, 141, 231)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Height = 45,
                Width = 250,
                Margin = new Thickness(0, 4, 0, 4),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Hand,
                Tag = edition
            };

            button.Clip = new RectangleGeometry
            {
                Rect = new Rect(0, 0, 250, 45),
                RadiusX = 8,
                RadiusY = 8
            };

            var transformGroup = new TransformGroup();
            transformGroup.Children.Add(new ScaleTransform(1, 1));
            transformGroup.Children.Add(new TranslateTransform(0, 0));
            button.RenderTransform = transformGroup;
            button.RenderTransformOrigin = new Point(0.5, 0.5);

            button.MouseLeftButtonUp += EditionButtonMain_Click;

            button.MouseEnter += (s, e) =>
            {
                var scale = (button.RenderTransform as TransformGroup)?.Children[0] as ScaleTransform;
                if (scale != null)
                {
                    var scaleAnim = new DoubleAnimation
                    {
                        To = 1.05,
                        Duration = TimeSpan.FromMilliseconds(200),
                        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                    };
                    scale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
                    scale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
                }
            };

            button.MouseLeave += (s, e) =>
            {
                var scale = (button.RenderTransform as TransformGroup)?.Children[0] as ScaleTransform;
                if (scale != null)
                {
                    var scaleAnim = new DoubleAnimation
                    {
                        To = 1.0,
                        Duration = TimeSpan.FromMilliseconds(200),
                        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                    };
                    scale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
                    scale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
                }
            };

            return button;
        }

        private async void EditionButtonMain_Click(object sender, MouseButtonEventArgs e)
        {
            if (!(sender is Label button))
                return;

            string selectedEdition = button.Tag?.ToString();
            if (string.IsNullOrEmpty(selectedEdition))
                return;

            // Product key input dialog
            var inputWindow = new Window
            {
                Title = "Product Key Required",
                Width = 450,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.ToolWindow,
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 48))
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });

            var label = new TextBlock
            {
                Text = $"Enter product key for {selectedEdition}:",
                Foreground = Brushes.White,
                Margin = new Thickness(15),
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetRow(label, 0);

            var textBox = new TextBox
            {
                Margin = new Thickness(15, 5, 15, 15),
                Padding = new Thickness(5),
                FontSize = 14,
                MaxLength = 29
            };
            Grid.SetRow(textBox, 1);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(15)
            };

            var okButton = new Button
            {
                Content = "OK",
                Width = 80,
                Height = 30,
                Margin = new Thickness(5),
                IsDefault = true
            };
            okButton.Click += (s, ev) => { inputWindow.DialogResult = true; };

            var cancelButton = new Button
            {
                Content = "Cancel",
                Width = 80,
                Height = 30,
                Margin = new Thickness(5),
                IsCancel = true
            };

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            Grid.SetRow(buttonPanel, 2);

            grid.Children.Add(label);
            grid.Children.Add(textBox);
            grid.Children.Add(buttonPanel);
            inputWindow.Content = grid;

            if (inputWindow.ShowDialog() == true)
            {
                string productKey = textBox.Text.Trim();
                if (string.IsNullOrWhiteSpace(productKey))
                {
                    MessageBox.Show("Product key cannot be empty.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Fade out edition panel, show output
                var fadeOut = new DoubleAnimation
                {
                    From = 1,
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(300),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
                };

                fadeOut.Completed += async (s2, e2) =>
                {
                    EditionSelectionPanel.Visibility = Visibility.Collapsed;
                    AdvancedOutputScrollViewer.Visibility = Visibility.Visible;
                    LoadingGIF.Visibility = Visibility.Visible;
                    StartLoadingGIF();

                    var fadeIn = new DoubleAnimation
                    {
                        From = 0,
                        To = 1,
                        Duration = TimeSpan.FromMilliseconds(300),
                        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                    };
                    AdvancedOutputScrollViewer.BeginAnimation(UIElement.OpacityProperty, fadeIn);

                    bool success = await _advancedToolsHelper.PerformEditionChange(selectedEdition, productKey);

                    StopLoadingGIF();
                    LoadingGIF.Visibility = Visibility.Collapsed;

                    if (success)
                    {
                        var result = MessageBox.Show(
                            "Edition change completed successfully!\n\nRestart now?",
                            "Success",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Information);

                        if (result == MessageBoxResult.Yes)
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = "shutdown",
                                Arguments = "/r /t 10 /c \"Restarting to apply Windows edition change\"",
                                CreateNoWindow = true,
                                UseShellExecute = false
                            });
                            Application.Current.Shutdown();
                        }
                    }
                };

                EditionSelectionPanel.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            }
        }

        private void CloseWindow(object sender, MouseButtonEventArgs e)
        {
            // Right-click to force close
            if (e.ChangedButton == MouseButton.Left && !_interactionEnabled)
            {
                HandleInvalidCloseClick();
                return;
            }

            // Get native top converted to WPF DIPs and apply optional tweak
            double currentTopDip = GetNativeWindowTop();

            // Clear any existing animation hold and set base value to the true visual top
            this.BeginAnimation(Window.TopProperty, null);
            this.Top = currentTopDip;

            // Clone storyboard and anchor the Top animation to the current DIP position
            var storyboardResource = (Storyboard)FindResource("SlideOutWindow");
            var sb = storyboardResource.Clone();

            foreach (var child in sb.Children)
            {
                if (child is DoubleAnimation da)
                {
                    var prop = Storyboard.GetTargetProperty(da);
                    string path = prop != null ? prop.Path : string.Empty;
                    if (path.Contains("Top") || path.Contains("(Window.Top)"))
                    {
                        da.From = currentTopDip;
                        da.To = currentTopDip + 50; // same movement as XAML; change if desired
                        da.By = null;
                        break;
                    }
                }
            }

            sb.Completed += (s, args) => Application.Current.Shutdown();
            sb.Begin(this);
        }

        [global::System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [global::System.Runtime.InteropServices.StructLayout(global::System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private double GetNativeWindowTop()
        {
            var helper = new System.Windows.Interop.WindowInteropHelper(this);
            var hWnd = helper.Handle;
            if (hWnd == IntPtr.Zero)
                return this.Top;

            if (GetWindowRect(hWnd, out RECT rect))
            {
                // Convert physical pixels to WPF device-independent units (DIPs)
                var source = PresentationSource.FromVisual(this);
                if (source?.CompositionTarget != null)
                {
                    var transform = source.CompositionTarget.TransformFromDevice;
                    var topDip = transform.Transform(new System.Windows.Point(rect.Left, rect.Top)).Y;
                    return topDip;
                }

                // fallback (assume 1:1)
                return rect.Top;
            }

            return this.Top;
        }

        private void HandleInvalidButtonClick(ContentControl button)
        {
            _invalidClickCount++;
            PlayErrorSound();

            // Start XAML wiggle storyboard targeted at the clicked button's TranslateTransform
            try
            {
                var wiggleRes = (Storyboard)FindResource("WiggleButton");
                var wiggle = wiggleRes.Clone();

                // Ensure any previous translate animation is cleared so we start from the real value
                button.BeginAnimation(TranslateTransform.XProperty, null);

                // Assign target for each child timeline to the clicked button
                foreach (Timeline child in wiggle.Children)
                    Storyboard.SetTarget(child, button);

                // Begin the animation (FillBehavior.Stop in XAML ensures no persistent value)
                wiggle.Begin(this, true);
            }
            catch
            {
                // ignore resource failures; sound still played
            }

            if (_invalidClickCount >= INVALID_CLICK_THRESHOLD)
            {
                MessageBox.Show("You can't do that right now. Please wait until the current task is finished.", "Wait", MessageBoxButton.OK, MessageBoxImage.Warning);
                _invalidClickCount = 0;
            }
        }

        private void HandleInvalidCloseClick()
        {
            _invalidClickCount++;
            PlayErrorSound();
            WiggleWindow();
            if (_invalidClickCount >= INVALID_CLICK_THRESHOLD)
            {
                MessageBox.Show("You can't close the window right now. Please wait until current task is finished.\nIf you really want to close it anyway, right-click the close button.", "Wait", MessageBoxButton.OK, MessageBoxImage.Warning);
                _invalidClickCount = 0;
            }
        }

        private void WiggleWindow()
        {
            // Prevent any running animations from changing window geometry while we animate.
            this.BeginAnimation(Window.LeftProperty, null);
            this.BeginAnimation(Window.TopProperty, null);

            // Capture native/top position in DIPs (same approach used in CloseWindow) to avoid OS/WPF re-centering.
            double currentTopDip = GetNativeWindowTop();
            this.Top = currentTopDip;

            double originalLeft = this.Left;

            var wiggle = new DoubleAnimationUsingKeyFrames
            {
                Duration = TimeSpan.FromMilliseconds(400),
                FillBehavior = FillBehavior.Stop // don't hold the animated value after completion
            };

            // Keyframes around the current Left value for a smooth wiggle
            wiggle.KeyFrames.Add(new EasingDoubleKeyFrame(originalLeft, KeyTime.FromPercent(0.0)));
            wiggle.KeyFrames.Add(new EasingDoubleKeyFrame(originalLeft - 10, KeyTime.FromPercent(0.2))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            });
            wiggle.KeyFrames.Add(new EasingDoubleKeyFrame(originalLeft + 10, KeyTime.FromPercent(0.4))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            });
            wiggle.KeyFrames.Add(new EasingDoubleKeyFrame(originalLeft - 7, KeyTime.FromPercent(0.6))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            });
            wiggle.KeyFrames.Add(new EasingDoubleKeyFrame(originalLeft, KeyTime.FromPercent(1.0))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            });

            Storyboard.SetTarget(wiggle, this);
            Storyboard.SetTargetProperty(wiggle, new PropertyPath("(Window.Left)"));

            var sb = new Storyboard();
            sb.Children.Add(wiggle);

            // Restore exact original coordinates (Top + Left) when done to avoid rounding/OS adjustments.
            sb.Completed += (s, e) =>
            {
                this.BeginAnimation(Window.LeftProperty, null);
                this.BeginAnimation(Window.TopProperty, null);
                this.Left = originalLeft;
                this.Top = currentTopDip;
            };

            sb.Begin(this);
        }

        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Normal)
            {
                var fadeInStoryboard = (Storyboard)FindResource("FadeInWindow");
                fadeInStoryboard.Begin(this);
            }
        }

        private void MinimizeWindow(object sender, MouseButtonEventArgs e)
        {
            var storyboard = (Storyboard)FindResource("FadeOutWindow");
            storyboard.Completed += (s, args) =>
            {
                WindowState = WindowState.Minimized;
                Opacity = 1;
            };
            storyboard.Begin(this);
        }

        private void Info(object sender, MouseButtonEventArgs e) => Process.Start(new ProcessStartInfo { FileName = "https://github.com/DynamiByte/UnGate11/blob/master/README.md" });

        private void DragWindow(object sender, MouseButtonEventArgs e) => DragMove();

        private void PlayErrorSound() => SystemSounds.Hand.Play();

        private Version GetVersion() => Assembly.GetExecutingAssembly().GetName().Version;
        

        // Resource Management
        private void ExtractResourceToFile(string resourceFileName, string destinationPath)
        {
            var assembly = Assembly.GetExecutingAssembly();
            string resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith(resourceFileName, StringComparison.OrdinalIgnoreCase));
            if (resourceName == null) return;

            using (var stream = assembly.GetManifestResourceStream(resourceName))
            using (var file = new FileStream(destinationPath, FileMode.Create, FileAccess.Write))
            {
                stream.CopyTo(file);
            }
        }
        

        // IDisposable Implementation
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Dispose managed resources
                    if (_gifTimer != null)
                    {
                        _gifTimer.Stop();
                        _gifTimer.Tick -= GifTimer_Tick;
                        _gifTimer = null;
                    }

                    if (_delayTimer != null)
                    {
                        _delayTimer.Stop();
                        _delayTimer = null;
                    }

                    _gifDecoder = null;

                    // Unsubscribe from events
                    if (_systemHelper != null)
                    {
                        _systemHelper.PatchStatusChecked -= SystemHelper_PatchStatusChecked;
                        _systemHelper.PatchActionCompleted -= SystemHelper_PatchActionCompleted;
                        _systemHelper.WindowsUpdateRefreshed -= SystemHelper_WindowsUpdateRefreshed;
                        _systemHelper.ProgressReported -= SystemHelper_ProgressReported;
                    }
                }
                _disposed = true;
            }
        }
        
    }
}