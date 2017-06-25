using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Windows.ApplicationModel.Core;
using Windows.Foundation;

namespace CurrencyEchange
{
    public class Header
    {
        public string fileName;
        public DateTime date;

        public string OnlyDate
        {
            get
            {
                return date.ToString("dd/MM/yyyy");
            }
        }
    }

    public class Currency
    {
        public string name { get; set; }
        public string sName { get; set; }
        public string coefficient { get; set; }
        public string value { get; set; }

        public string summary
        {
            get
            {
                return $"{sName}\nPrzelicznik:{coefficient}\nKurs Sredni:{value}";
            }
        }
        public override string ToString()
        {
            return $"{name} {sName}\nPrzelicznik:{coefficient}\nKurs Sredni:{value}";
        }
    }
    public class CurrencyData : INotifyPropertyChanged
    {
        Header currentlySelected;

        public Header CurrentlySelectedHeader
        {
            set
            {
                currentlySelected = value;
                this.OnPropertyChanged();
            }
            get
            {
                return currentlySelected;
            }
        }
        public event PropertyChangedEventHandler PropertyChanged = delegate { };

        private const string fileNameSource = "http://www.nbp.pl/kursy/xml/dir.txt";
        const int expectedFilenameLength = 11;
        public ObservableCollection<Header> headers = new ObservableCollection<Header>();
        public ObservableCollection<Currency> currencies;
        private static HttpClient httpClient = new HttpClient();

        public void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            this.PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }

        public IAsyncOperationWithProgress<bool, String> downloadXmlFile(string filename, CancellationToken cToken)
        {
            string url = "http://www.nbp.pl/kursy/xml/" + filename + ".xml";

            return AsyncInfo.Run<bool, string>((token, progress) => Task.Run(
                 async () =>
                 {
                     var request = new HttpRequestMessage(HttpMethod.Get, url);
                     request.Headers.Add("Accept", "text/xml;charset=utf-8");
                     request.Headers.Add("Accept-Charset", "utf-8");
                     var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, cToken);
                     var rString = await response.Content.ReadAsStringAsync();
                     
                     if (response.StatusCode != System.Net.HttpStatusCode.OK)
                     {
                         progress.Report("Error during XML download!");
                         return false;
                     }

                     progress.Report("file downloaded, parsing XML...");

                     var curList = XML_Parser(rString);
                     if (currencies != null)
                         await CoreApplication.MainView.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                         {
                             currencies.Clear();
                         });

                     currencies = new ObservableCollection<Currency>(curList);
                     request.Dispose();

                     progress.Report("XML parsed succesfully!");

                     return false;

                 }));
        }

        public async Task<bool> downloadXml(string filename)
        {
            string url = "http://www.nbp.pl/kursy/xml/" + filename + ".xml";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            var response = await httpClient.SendAsync(request);

            var rString = await response.Content.ReadAsStringAsync();

            var curList = XML_Parser(rString);
            if (currencies != null)
                await CoreApplication.MainView.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    currencies.Clear();
                });

            currencies = new ObservableCollection<Currency>(curList);
            request.Dispose();
            return false;
        }
        private IEnumerable<Currency> XML_Parser(string xmlString)
        {
            XDocument doc = XDocument.Parse(xmlString);
            try
            {
                IEnumerable<Currency> query = from pozycja in doc.Root.Elements("pozycja")
                                              select new Currency
                                              {
                                                  name = (string)pozycja.Element("nazwa_waluty"),
                                                  sName = (string)pozycja.Element("kod_waluty"),
                                                  coefficient = (string)pozycja.Element("przelicznik"),
                                                  value = (string)pozycja.Element("kurs_sredni")
                                              };
                return query;
            }
            catch (Exception ex)
            {
                //To do, handle wrong page or sth
                return null;
            }

        }

        public IAsyncOperationWithProgress<ObservableCollection<Header>, string> downloadFileNames(CancellationToken cToken)
        {
            return AsyncInfo.Run<ObservableCollection<Header>, string>((token, progress) => Task.Run(
                async () =>
                {
                    if (cToken != CancellationToken.None)
                    {
                        token = cToken;
                    }
                    ObservableCollection<Header> result = new ObservableCollection<Header>();
                    var request = new HttpRequestMessage(HttpMethod.Get, fileNameSource);
                    var response = httpClient.SendAsync(request, token);


                    progress.Report("Connected to nbp, parsing ...");
                    StringBuilder res = new StringBuilder(expectedFilenameLength);
                    int c = 0;

                    try
                    {
                        var resp = await response;
                        if (resp.StatusCode != System.Net.HttpStatusCode.OK)
                        {
                            progress.Report("Could not connect!");
                            return result;
                        }

                        var resStream = await response.Result.Content.ReadAsStreamAsync();
                        while (c != -1)
                        {
                            token.ThrowIfCancellationRequested();
                            c = resStream.ReadByte();
                            if ((char.IsLetterOrDigit((char)c) && c <= 127))
                                res.Append((char)c);

                            if ((char)c == '\n')
                            {
                                var parsedObj = parseFileName(res.ToString());
                                if (parsedObj != null)
                                     result.Add(parsedObj);


                                res.Clear();
                            }
                            progress.Report("parsed!");
                        }
                        resStream.Dispose();
                    }
                    catch (Exception ex)
                    {
                        // To Do: exepctions
                        System.Diagnostics.Debug.WriteLine(ex.Message);
                        return result;
                    }
                    return result;
                }, token));

        }

        private Header parseFileName(string filename)
        {
            Header result = new Header();
            DateTime date;
            string dateStr;
            if (filename.Length < expectedFilenameLength)
                return null;
            else if (filename.First() != 'a')
                return null;
            try
            {
                dateStr = filename.Substring(5);
                int year = 0;
                int month = 0;
                int day = 0;
                Int32.TryParse(dateStr.Substring(0, 2), out year);
                Int32.TryParse(dateStr.Substring(2, 2), out month);
                Int32.TryParse(dateStr.Substring(4, 2), out day);
                year += 2000;

                date = new DateTime(year, month, day);
                result.fileName = filename;
                result.date = date;
            }
            catch (Exception ex)
            {
                //TO DO, handling exeptions
            }

            return result;
        }

    }
}
