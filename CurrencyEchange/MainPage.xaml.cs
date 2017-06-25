using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace CurrencyEchange
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public CurrencyData cData;
        private CancellationTokenSource m_CancellationSource = new CancellationTokenSource();
        IAsyncOperationWithProgress<ObservableCollection<Header>, string> downloadOperation = null;
        bool active = false;
        public MainPage()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = NavigationCacheMode.Enabled;
            cData = new CurrencyData();
            Header storedData = loadStoredHeader();
            if(AppBackup.appState[AppBackup.currencyDataKey] != null)
            {
                cData = (CurrencyData)AppBackup.appState[AppBackup.currencyDataKey];
            }
            else if (storedData != null)
            {
                cData.headers.Add(storedData);
                //listViewDates.SelectedItem = listViewDates.Items.Last();
            }
            
        }

        private async void buttonDownloadDates_Click(object sender, RoutedEventArgs e)
        {

            cData.headers.Clear();

            if (downloadOperation != null)
            {
                m_CancellationSource.Cancel();
                m_CancellationSource.Dispose();
                m_CancellationSource = new CancellationTokenSource();
            }
            try {
                downloadOperation = cData.downloadFileNames(m_CancellationSource.Token);
                downloadOperation.Progress = (result, progress) =>
                {
                    textBlockStatus.Text = progress;
                };
                var headers = await downloadOperation;
                foreach (Header el in headers)
                {
                    cData.headers.Add(el);
                }

                listViewDates.SelectedItem = listViewDates.Items.Last();
                AppBackup.appState[AppBackup.currencyDataKey] = cData;
            }
            catch(Exception ex)
            {
                textBlockStatus.Text = "Download Cancelled!";
            }

        }

        Header loadStoredHeader()
        {
            Header result = null;
            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
            ApplicationDataCompositeValue composite = (ApplicationDataCompositeValue)localSettings.Values["mainPageSettings"];
            if (composite != null)
            {
                result = new Header();
                result.date = ((DateTimeOffset) composite["dateH"]).DateTime;
                result.fileName = (string) composite["fileName"];
            }

            return result;
        }

        void storeSelectedHeader(Header header)
        {
            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
            ApplicationDataCompositeValue composite = new ApplicationDataCompositeValue();
            composite["dateH"] = (DateTimeOffset)header.date;
            composite["fileName"] = header.fileName;
            localSettings.Values["mainPageSettings"] = composite;
        }

        private async void listViewDates_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Header item = (Header)listViewDates.SelectedItem;
            if (item != null)
            {
                storeSelectedHeader(item);
                AppBackup.appState[AppBackup.currencyDataKey] = cData;
                if (active == false)
                {
                    active = true;
                    try
                    {
                        var operation = cData.downloadXmlFile(item.fileName, m_CancellationSource.Token);
                        operation.Progress = (result, progress) => { textBlockStatus.Text = progress; };
                        active = await operation;
                        this.listViewCurrencies.ItemsSource = cData.currencies;
                       // textBlockDate.Text = item.date.ToString("dd /MM yyyy");
                        cData.CurrentlySelectedHeader = item;

                        CurrencyHistoryView.availableCurrencies.Clear();
                        foreach (Currency c in cData.currencies)
                        {
                            CurrencyHistoryView.availableCurrencies.Add(c.sName);
                        }
                    }
                    catch(Exception ex)
                    {

                    }

                }
            }


        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
            localSettings.Values["currentPage"] = (int)ActivePage.mainPage;

            base.OnNavigatedTo(e);
        }
        private void listViewCurrencies_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedCurrency = (Currency)listViewCurrencies.SelectedItem;
            if(selectedCurrency != null)
                Frame.Navigate(typeof(HistoryPage), selectedCurrency.sName);
        }

        private void AppBarButton_Click(object sender, RoutedEventArgs e)
        {
            App.Current.Exit();
        }
    }
}
