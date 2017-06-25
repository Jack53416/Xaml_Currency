using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Windows.ApplicationModel.Core;
using Windows.Foundation;


namespace CurrencyEchange
{
    public class CurrencyHistory
    {
        public DateTime date { get; set; }

        public string Date { get { return date.ToString(@"dd\MM\yyyy"); } }
        public double value { get; set; }
    }

    public class CurrencyHistoryView
    {
        public string CurrencyName { get; set; }

        public ObservableCollection<CurrencyHistory> log = new ObservableCollection<CurrencyHistory>();

        public static ObservableCollection<string> availableCurrencies = new ObservableCollection<string>();

        private static HttpClient httpClient = new HttpClient();


        async Task<IEnumerable<CurrencyHistory>> downloadXml(string url, CancellationToken cToken)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Accept", "text/xml");
            IEnumerable<CurrencyHistory> query = null;

            try
            {
                var response = await httpClient.SendAsync(request,cToken);
                if (cToken.IsCancellationRequested)
                    return null;

                var rString = await response.Content.ReadAsStringAsync();

                if (response.StatusCode != System.Net.HttpStatusCode.OK)
                    throw new Exception("Status code other than 200");
                XDocument doc = XDocument.Parse(rString);
                query = from Rate in doc.Root.Element("Rates").Elements("Rate")
                        select new CurrencyHistory
                        {
                            date = Convert.ToDateTime((string)Rate.Element("EffectiveDate")),
                            value = (double)Rate.Element("Mid")
                        };


            }
            catch (Exception ex)
            {
                throw new Exception("Error during XML download: " + ex.Message);
            }
            return query;
        }
        public IAsyncActionWithProgress<int> downloadCurrencyLog(string currencyName, DateTimeOffset dateFrom, DateTimeOffset dateTo, CancellationToken cToken)
        {
            List<string> urls;
            List<Task<IEnumerable<CurrencyHistory>>> tasks = new List<Task<IEnumerable<CurrencyHistory>>>();

            return AsyncInfo.Run<int>((token, progress) => Task.Run(async () =>
            {
                if(cToken != CancellationToken.None)
                {
                    token = cToken;
                }
                urls = createUrls(dateFrom, dateTo, 367, currencyName);
                foreach (var u in urls)
                {
                    tasks.Add(Task.Run(() => { return downloadXml(u, token); }));
                }
                int finishedTasks = 0;

                if (token.IsCancellationRequested)
                    throw new Exception("download cancelled");
                foreach (var t in tasks)
                {
                    await t.ContinueWith(async completed =>
                    {
                        switch (completed.Status)
                        {
                            case TaskStatus.RanToCompletion:
                                if (completed.Result != null)
                                {
                                    finishedTasks++;
                                    progress.Report((int)(((double)finishedTasks / tasks.Count) * 100.0));
                                    await CoreApplication.MainView.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                                    {

                                        foreach (var ch in completed.Result)
                                        {
                                            log.Add(ch);
                                        }
                                    });
                                }
                                break;
                            case TaskStatus.Faulted:; break;
                        }
                    }, TaskScheduler.Default);
                }
                await Task.WhenAll(tasks);
            }));

        }


        List<string> createUrls(DateTimeOffset dateFrom, DateTimeOffset dateTo, int daysChunk, string currencyName)
        {
            List<string> result = new List<string>();
            List<DateTimeOffset> dates = new List<DateTimeOffset>();
            int dayCount = (dateTo - dateFrom).Days;
            string url;
            dates.Add(dateFrom);
            while (dayCount > daysChunk)
            {
                DateTimeOffset d = dates.Last().Add(new TimeSpan(daysChunk, 0, 0, 0));
                dayCount -= daysChunk;
                dates.Add(d);
            }
            dates.Add(dateTo);
            for (int i = 1; i < dates.Count; ++i)
            {
                url = $"http://api.nbp.pl/api/exchangerates/rates/a/{currencyName}/{dates[i - 1].ToString("yyyy-MM-dd")}/{dates[i].ToString("yyyy-MM-dd")}";
                System.Diagnostics.Debug.WriteLine(url);
                dates[i].Add(new TimeSpan(1, 0, 0, 0));
                result.Add(url);
            }
            return result;
        }

    }
}
