using Syncfusion.UI.Xaml.Charts;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace CurrencyEchange
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    /// 
    enum ActivePage {mainPage,historyPage};
   
    public sealed partial class HistoryPage : Page
    {
        private CurrencyHistoryView currencyView;
        private CancellationTokenSource m_cancellationSource;
        IAsyncActionWithProgress<int> downloadAction = null;

        public HistoryPage()
        {
            this.InitializeComponent();
            this.comboBoxCurNames.ItemsSource = CurrencyHistoryView.availableCurrencies;
            loadUserData();

            DatePickerFrom.MinYear = new DateTimeOffset(new DateTime(2002, 01, 02));
            currencyView = new CurrencyHistoryView();
            m_cancellationSource = new CancellationTokenSource();
        }


        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.Parameter is string && !string.IsNullOrWhiteSpace((string)e.Parameter))
            {
                currencyView.CurrencyName = e.Parameter.ToString();
                comboBoxCurNames.SelectedItem = currencyView.CurrencyName;
                ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
                localSettings.Values["currentPage"] = (int)ActivePage.historyPage;
            }
            base.OnNavigatedTo(e);
        }

        private void loadUserData()
        {
            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;

            ApplicationDataCompositeValue composite = (ApplicationDataCompositeValue)localSettings.Values["chartPageSettings"];
            if (composite != null)
            {
                DatePickerFrom.Date = (DateTimeOffset)composite["dateFrom"];
                DatePickerTo.Date = (DateTimeOffset)composite["dateTo"];
                lineChart.Series[0].Label = (string)composite["cName"];
                loadLog();
            }
        }
        private async void loadLog()
        {
            StorageFile localFile;
            var localFolder = ApplicationData.Current.LocalFolder;
            try
            {
                localFile = await localFolder.GetFileAsync("charData.xml.");
            }
            catch (FileNotFoundException ex)
            {
                localFile = null;
            }
            if (localFile != null)
            {
                string localData = await FileIO.ReadTextAsync(localFile);
                currencyView.log = ObjectSerializer<ObservableCollection<CurrencyHistory>>.FromXml(localData);
                lineChart.Series[0].ItemsSource = currencyView.log;
                lineChart.Header = $"{lineChart.Series[0].Label} Exchange rate from previous session";
            }
            else
                lineChart.Header = $"{currencyView.CurrencyName} Exchange rate";

            if(CurrencyHistoryView.availableCurrencies.Count == 0)
            {
                try
                {
                    localFile = await localFolder.GetFileAsync("options.xml.");
                }
                catch (FileNotFoundException ex)
                {
                    localFile = null;
                }

                if(localFile != null)
                {
                    string localData = await FileIO.ReadTextAsync(localFile);
                    System.Diagnostics.Debug.WriteLine(currencyView.CurrencyName);
                    var loadedOptions = ObjectSerializer<ObservableCollection<string>>.FromXml(localData);
                    foreach(var option in loadedOptions)
                    {
                        CurrencyHistoryView.availableCurrencies.Add(option);
                        comboBoxCurNames.SelectedItem = lineChart.Series[0].Label;
                    }

                }
            }

        }


        private void storeUserData()
        {
            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
            ApplicationDataCompositeValue composite = new ApplicationDataCompositeValue();
            composite["dateFrom"] = DatePickerFrom.Date;
            composite["dateTo"] = DatePickerTo.Date;
            composite["cName"] = currencyView.CurrencyName;
            localSettings.Values["chartPageSettings"] = composite;
        }

        private async void storeChartData()
        {
            StorageFolder localFolder = ApplicationData.Current.LocalFolder;
            string rootFrameDataString = ObjectSerializer<ObservableCollection<CurrencyHistory>>.ToXml(currencyView.log);
            if (!string.IsNullOrEmpty(rootFrameDataString))
            {
                StorageFile file = await localFolder.CreateFileAsync("charData.xml", CreationCollisionOption.ReplaceExisting);
                await FileIO.WriteTextAsync(file, rootFrameDataString);
            }

            string optionsframeDataString = ObjectSerializer<ObservableCollection<string>>.ToXml(CurrencyHistoryView.availableCurrencies);
            if(!string.IsNullOrEmpty(optionsframeDataString))
            {
                StorageFile file = await localFolder.CreateFileAsync("options.xml", CreationCollisionOption.ReplaceExisting);
                await FileIO.WriteTextAsync(file, optionsframeDataString);
            }
        }
        private async void buttonStart_Click(object sender, RoutedEventArgs e)
        {
            textBlockError.Text = "";
            lineChart.Header = lineChart.Header = $"{currencyView.CurrencyName} Exchange rate";
            lineChart.Series[0].ItemsSource = currencyView.log;
            lineChart.SuspendSeriesNotification();

            if (downloadAction != null)
            {
                m_cancellationSource.Cancel();
                m_cancellationSource.Dispose();
                m_cancellationSource = new CancellationTokenSource();
            }

            if (areDatesCorrect())
            {
                storeUserData();
                lineChart.Series[0].Label = currencyView.CurrencyName;


                try
                {
                    progressBar.Value = 0;
                    progressBar.Visibility = Visibility.Visible;
                    currencyView.log.Clear();
                    downloadAction = currencyView.downloadCurrencyLog(currencyView.CurrencyName, DatePickerFrom.Date, DatePickerTo.Date, m_cancellationSource.Token);
                    downloadAction.Progress = (result, progress) => { progressBar.Value = progress; };
                    await downloadAction;

                    storeChartData();
                    progressBar.Visibility = Visibility.Collapsed;
                }
                catch (Exception ex)
                {
                    textBlockError.Text = ex.Message;
                    progressBar.Visibility = Visibility.Collapsed;
                }

                lineChart.ResumeSeriesNotification();

            }
        }

        private bool areDatesCorrect()
        {
            var background = new SolidColorBrush(Colors.White);

            if (DatePickerFrom.Date.Date >= DatePickerTo.Date.Date)
            {
                DatePickerTo.Background = new SolidColorBrush(Colors.Red);
                DatePickerFrom.Background = new SolidColorBrush(Colors.Red);
                textBlockError.Text = "DateTo is equal or before DateFrom!";
                return false;
            }
            if (DatePickerTo.Date.Date> DateTime.Today.Date)
            {
                DatePickerTo.Background = new SolidColorBrush(Colors.Red);
                textBlockError.Text = "Date To is set above current time!";
                return false;
            }
            System.Diagnostics.Debug.WriteLine((DatePickerFrom.Date.CompareTo(DatePickerTo.Date)));
            DatePickerFrom.Background = background;
            DatePickerTo.Background = background;
            return true;
        }



        private async void AppBarButton_Click(object sender, RoutedEventArgs e)
        {
            if (canvasGraph != null)
            {
                var picker = new FileSavePicker();
                picker.FileTypeChoices.Add("PNG image", new string[] { ".png" });
                StorageFile file = await picker.PickSaveFileAsync();
                if (file != null)
                {
                    var renderer = new RenderTargetBitmap();
                    await renderer.RenderAsync(canvasGraph);
                    var pixels = await renderer.GetPixelsAsync();
                    IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.ReadWrite);
                    var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
                    var bytes = pixels.ToArray();
                    encoder.SetPixelData(BitmapPixelFormat.Bgra8,
                                            BitmapAlphaMode.Straight,
                                            (uint)renderer.PixelWidth, (uint)renderer.PixelHeight,
                                            96, 96, bytes);

                    await encoder.FlushAsync();

                }
            }
        }

        private void AppBarButton_Click_1(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(MainPage));
        }

        private void AppBarButton_Click_2(object sender, RoutedEventArgs e)
        {
            App.Current.Exit();
        }

        private void AppBarToggleButton_Checked(object sender, RoutedEventArgs e)
        {
            ChartTrackBallBehavior behavior = new ChartTrackBallBehavior();

            lineChart.Behaviors.Add(behavior);
            lineChart.PrimaryAxis.ShowTrackBallInfo = true;



        }

        private void AppBarToggleButton_Unchecked(object sender, RoutedEventArgs e)
        {
            lineChart.PrimaryAxis.ShowTrackBallInfo = false;
            lineChart.Behaviors.Remove(lineChart.Behaviors.Last());
        }

        private void comboBoxCurNames_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if(comboBoxCurNames.SelectedItem != null)
                currencyView.CurrencyName = (string) comboBoxCurNames.SelectedItem;
        }
    }

}
