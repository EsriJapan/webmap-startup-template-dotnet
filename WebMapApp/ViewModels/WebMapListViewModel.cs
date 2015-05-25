using Esri.ArcGISRuntime.Portal;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using WebMapApp.Common;
using WebMapApp.Views;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace WebMapApp.ViewModels
{
    /// <summary>
    /// Web マップ一覧ページのビューモデル
    /// </summary>
    public class WebMapListViewModel : ViewModelBase
    {
        /// <summary>
        /// コンストラクタ
        /// </summary>
        public WebMapListViewModel()
        {
            //コマンドの初期亜k
            _searchCommand = new RelayCommand(async () => await SearchArcgisOnline());
            _openWebMapCommand = new RelayCommand(() => OpenWebMap());

            //検索結果コレクションの初期化
            _searchResults = new ObservableCollection<ArcGISPortalItem>();

            //
            var task = GetFeaturedWebMapsAsync();
        }

        //検索文字列
        private string _searchText;
        public string SearchText
        {
            get { return _searchText; }
            set
            {
                _searchText = value;
                OnPropertyChanged();
            }
        }

        //Web マップ 検索結果のコレクション
        private ObservableCollection<ArcGISPortalItem> _searchResults;
        public ObservableCollection<ArcGISPortalItem> SearchResults
        {
            get { return _searchResults; }
            set
            {
                _searchResults = value;
                OnPropertyChanged();
            }
        }

        //選択された Web マップ
        private ArcGISPortalItem _selectedPortalItem;
        public ArcGISPortalItem SelectedPortalItem
        {
            get { return _selectedPortalItem; }
            set
            {
                _selectedPortalItem = value;
                _openWebMapCommand.RaiseCanExecuteChanged();
                OnPropertyChanged();
            }
        }

        //Web マップ検索の実行
        private RelayCommand _searchCommand;
        public RelayCommand SearchCommand { get { return _searchCommand; } }

        //Web マップを開く
        private RelayCommand _openWebMapCommand;
        public RelayCommand OpenWebMapCommand { get { return _openWebMapCommand; } }

        //ArcGIS ポータル
        private ArcGISPortal _portal;

        /// <summary>
        /// ArcGIS Online から平均評価準で Web マップを検索
        /// </summary>
        private async Task GetFeaturedWebMapsAsync()
        {
            try
            {
                //処理開始
                IsBusy = true;

                //ArcGIS Online ポータルインスタンスを作成
                if (_portal == null)
                    _portal = await ArcGISPortal.CreateAsync();

                //検索パラメーターを初期化（最大 20 件、評価の高い順でソート）
                var searchParams = new SearchParameters("type: \"web map\" NOT \"web mapping application\" ")
                {
                    Limit = 20,
                    SortField = "avgrating",
                    SortOrder = QuerySortOrder.Descending,
                };

                //検索を実行
                var result = await _portal.ArcGISPortalInfo.SearchFeaturedItemsAsync();

                //検索結果を表示
                SearchResults = new ObservableCollection<ArcGISPortalItem>(result.Results);
            }
            catch (Exception ex)
            {
                //エラー通知
                var _ = new MessageDialog(ex.Message).ShowAsync();
            }
            finally
            {
                //処理終了
                IsBusy = false;
            }
        }

        /// <summary>
        /// 検索文字列で Web マップを検索
        /// </summary>
        private async Task SearchArcgisOnline()
        {
            try
            {
                //処理開始
                IsBusy = true;

                //ArcGIS Online ポータルインスタンスを作成
                SearchResults.Clear();

                if (_portal == null)
                    _portal = await ArcGISPortal.CreateAsync();

                //検索文字列が指定されていれば検索
                if (!string.IsNullOrEmpty(SearchText))
                {
                    //検索パラメーターを初期化（検索文字列と一致する Web マップ、最大 20 件、評価の高い順でソート）
                    var searchParams = new SearchParameters(SearchText + " type: \"web map\" NOT \"web mapping application\" ")
                    {
                        Limit = 20,
                        SortField = "avgrating",
                        SortOrder = QuerySortOrder.Descending,
                    };

                    //検索を実行
                    var result = await _portal.SearchItemsAsync(searchParams);

                    //検索結果を表示
                    SearchResults = new ObservableCollection<ArcGISPortalItem>(result.Results);
                }
                //検索文字列が指定されていない場合は平均評価準で Web マップを検索
                else
                {
                    await GetFeaturedWebMapsAsync();
                }
            }
            catch (Exception ex)
            {
                //エラー通知
                var _ = new MessageDialog(ex.Message).ShowAsync();
            }
            finally
            {
                //処理終了
                IsBusy = false;
            }
        }

        //Web マップページに遷移し、選択された Web マップを開く
        private void OpenWebMap()
        {
            //Web マップが選択されていなければ戻る
            if (SelectedPortalItem == null) return;

            //マップページに遷移
            Frame rootFrame = Window.Current.Content as Frame;
            rootFrame.Navigate(typeof(MapView), SelectedPortalItem);
        }
    }
}
