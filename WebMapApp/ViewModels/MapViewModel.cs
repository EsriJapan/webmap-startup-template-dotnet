using Esri.ArcGISRuntime.Controls;
using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Layers;
using Esri.ArcGISRuntime.Portal;
using Esri.ArcGISRuntime.Symbology;
using Esri.ArcGISRuntime.Tasks.Geoprocessing;
using Esri.ArcGISRuntime.Tasks.Query;
using Esri.ArcGISRuntime.WebMap;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using WebMapApp.Common;
using WebMapApp.Models;
using Windows.UI;
using Windows.UI.Popups;
using Windows.UI.Xaml;

namespace WebMapApp.ViewModels
{
    public class MapViewModel : ViewModelBase
    {
        //ArcGIS Online 到達圏解析ジオプロセシング サービスの URL
        private const string SERVICE_AREA_GP_URL = "http://logistics.arcgis.com/arcgis/rest/services/World/ServiceAreas/GPServer/GenerateServiceAreas";

        private const string MAXVALFIELD = "max_value";                 //最大値フィールド名
        private const string MINVALFIELD = "min_value";                 //最小値フィールド名

        private ArcGISPortal _portal;                                   //ポータルオブジェクト
        private string _oidFieldName;                                   //オブジェクトIDのフィールド名
        private string _originalFilter;                                 //処理対象レイヤ取得時のフィルタ条件文字列
        private DispatcherTimer _updateFilterTimer;                     //スライダーフィルタ更新用タイマー
        private Geoprocessor _serviceAreaGp;                            //到達圏解析用のジオプロセシング タスク
        private Graphic _serviceAreaGraphic;                            //到達圏グラフィック
        private bool _canExecuteSAAnalysis;                             //到達圏解析が実行可能か判定するフラグ
        private GraphicsOverlay _serviceAreaResultOverlay;              //到達圏解析結果表示用オーバーレイ
        private GraphicsOverlay _serviceAreaStartPointOverlay;          //到達圏解析開始ポイントオーバーレイ
        private GraphicsOverlay _selectedFeatureOverlay;                //選択されているフィーチャ表示用オーバーレイ

        public MapViewModel(ArcGISPortalItem portalItem)
        {
            //処理対象レイヤ候補のリストを初期化
            _targetLayerCollection = new ObservableCollection<Layer>();

            //処理対象フィールド候補のリストを初期化
            _targetFieldCollection = new ObservableCollection<Esri.ArcGISRuntime.Data.FieldInfo>();

            #region コマンドの初期化
            _setTargetFieldCollectionCommand = new RelayCommand(SetTargetFieldCollection);
            _setTargetFiledCommand = new RelayCommand(SetTargetFiled);
            _updateFilterCommand = new RelayCommand(UpdateFilter);
            _enableSAAnalysisCommand = new RelayCommand(EnableServiceAreaAnalysis, CanEnableServiceAreaAnalysis);
            _executeSAAnalysisCommand = new RelayCommand<MapViewInputEventArgs>(async (MapViewInputEventArgs e) => await ExecuteServiceAreaAnalysis(e));
            #endregion

            //到達圏解析用のジオプロセシング タスクを初期化
            _serviceAreaGp = new Geoprocessor(new Uri(SERVICE_AREA_GP_URL));

            #region グラフィック オーバーレイの初期化
            //到達圏解析結果表示用オーバーレイの初期化
            _serviceAreaResultOverlay = new GraphicsOverlay()
            {
                Renderer = new SimpleRenderer()
                {
                    Symbol = new SimpleFillSymbol()
                    {
                        Color = Colors.Green,
                    },
                },
                Opacity = 0.7,
            };

            //到達圏解析開始ポイントオーバーレイの初期化
            _serviceAreaStartPointOverlay = new GraphicsOverlay()
            {
                Renderer = new SimpleRenderer()
                {
                    Symbol = new SimpleMarkerSymbol()
                    {
                        Size = 12,
                        Style = SimpleMarkerStyle.Square,
                        Color = Color.FromArgb(225, 0, 255, 0),
                    },
                },
            };

            //選択されているフィーチャ表示用オーバーレイの初期化
            _selectedFeatureOverlay = new GraphicsOverlay()
            {
                Renderer = new SimpleRenderer()
                {
                    Symbol = new SimpleMarkerSymbol()
                    {
                        Size = 18,
                        Style = SimpleMarkerStyle.Circle,
                        Color = Color.FromArgb(225, 255, 0, 0),
                    },
                },
            };

            //結果表示用オーバーレイコレクションを初期化し各オーバーレイを追加
            _resultOverlayCollection = new GraphicsOverlayCollection();
            _resultOverlayCollection.Add(_serviceAreaResultOverlay);
            _resultOverlayCollection.Add(_serviceAreaStartPointOverlay);
            _resultOverlayCollection.Add(_selectedFeatureOverlay);
            #endregion

            //グラフアイテムのコレクションを初期化
            _chartItemCollection = new ObservableCollection<ChartItem>();

            //選択されたポータルアイテムの取得
            this.LoadedPortalItem = portalItem;

            //Web マップを読み込み表示
            var _ = LoadWebMapAsync();
        }

        #region プロパティ
        private ArcGISPortalItem _loadedPortalItem;
        /// <summary>
        /// 読み込まれたポータル アイテム
        /// </summary>
        public ArcGISPortalItem LoadedPortalItem
        {
            get { return _loadedPortalItem; }
            private set
            {
                _loadedPortalItem = value;
                OnPropertyChanged();
            }
        }

        private WebMapViewModel _currentWebMapVM;
        /// <summary>
        /// Web マップビューモデル
        /// </summary>
        public WebMapViewModel CurrentWebMapVM
        {
            get { return _currentWebMapVM; }
            set
            {
                _currentWebMapVM = value;
                OnPropertyChanged();
            }
        }

        public GraphicsOverlayCollection _resultOverlayCollection;
        /// <summary>
        /// 結果表示用オーバーレイコレクション 
        /// </summary>
        public GraphicsOverlayCollection ResultOverlayCollection
        {
            get { return _resultOverlayCollection; }
            set
            {
                _resultOverlayCollection = value;
                OnPropertyChanged();
            }
        }

        private bool _isMapReady;
        /// <summary>
        /// マップ読み込み完了を示すフラグ
        /// </summary>
        public bool IsMapReady
        {
            get { return _isMapReady; }
            set
            {
                _isMapReady = value;
                OnPropertyChanged();
            }
        }

        private ObservableCollection<Layer> _targetLayerCollection;
        /// <summary>
        /// 処理対象レイヤ候補のリスト
        /// </summary>
        public ObservableCollection<Layer> TargetLayerCollection
        {
            get { return _targetLayerCollection; }
            set
            {
                _targetLayerCollection = value;
                OnPropertyChanged();
            }
        }

        private FeatureLayer _targetLayer;
        /// <summary>
        /// 処理対処レイヤ
        /// </summary>
        public FeatureLayer TargetLayer
        {
            get { return _targetLayer; }
            set
            {
                _targetLayer = value;
                OnPropertyChanged();

                //レイヤ取得時のフィルタ条件を保持
                _originalFilter = ((ServiceFeatureTable)_targetLayer.FeatureTable).Where;
            }
        }

        private bool _isTargetLayerSelected;
        /// <summary>
        /// 処理対処レイヤ選択完了を示すフラグ
        /// </summary>
        public bool IsTargetLayerSelected
        {
            get { return _isTargetLayerSelected; }
            set
            {
                _isTargetLayerSelected = value;
                OnPropertyChanged();
            }
        }

        private ObservableCollection<Esri.ArcGISRuntime.Data.FieldInfo> _targetFieldCollection;
        /// <summary>
        /// 処理対象フィールド候補のリスト
        /// </summary>
        public ObservableCollection<Esri.ArcGISRuntime.Data.FieldInfo> TargetFieldCollection
        {
            get { return _targetFieldCollection; }
            set
            {
                _targetFieldCollection = value;
                OnPropertyChanged();
            }
        }

        private Esri.ArcGISRuntime.Data.FieldInfo _targetField;
        /// <summary>
        /// 処理対処フィールド
        /// </summary>
        public Esri.ArcGISRuntime.Data.FieldInfo TargetField
        {
            get { return _targetField; }
            set
            {
                _targetField = value;
                OnPropertyChanged();
            }
        }

        private bool _isTargetFieldSelected;
        /// <summary>
        /// 処理対処フィールド選択完了を示すフラグ
        /// </summary>
        public bool IsTargetFieldSelected
        {
            get { return _isTargetFieldSelected; }
            set
            {
                _isTargetFieldSelected = value;
                OnPropertyChanged();

                //コマンドの実行可否判定更新
                EnableSAAnalysisCommand.RaiseCanExecuteChanged();
            }
        }

        private double _maxFilterValue;
        /// <summary>
        /// フィルタ設定の最大値
        /// </summary>
        public double MaxFilterValue
        {
            get { return _maxFilterValue; }
            set
            {
                _maxFilterValue = value;
                OnPropertyChanged();
            }
        }

        private double _minFilterValue;
        /// <summary>
        /// フィルタ設定の最小値
        /// </summary>
        public double MinFilterValue
        {
            get { return _minFilterValue; }
            set
            {
                _minFilterValue = value;
                OnPropertyChanged();
            }
        }

        private double _currentFilterValue;
        /// <summary>
        /// フィルタ設定の現在値
        /// </summary>
        public double CurrentFilterValue
        {
            get { return _currentFilterValue; }
            set
            {
                _currentFilterValue = value;
                OnPropertyChanged();
            }
        }

        private bool _isExecutingSAAnalysis;
        /// <summary>
        /// 到達圏解析を実行中か示すフラグ
        /// </summary>
        public bool IsExecutingSAAnalysis
        {
            get { return _isExecutingSAAnalysis; }
            set
            {
                _isExecutingSAAnalysis = value;
                OnPropertyChanged();

                //コマンドの実行可否判定更新
                EnableSAAnalysisCommand.RaiseCanExecuteChanged();
            }
        }

        private ObservableCollection<ChartItem> _chartItemCollection;
        /// <summary>
        /// グラフ アイテムのコレクション
        /// </summary>
        public ObservableCollection<ChartItem> ChartItemCollection
        {
            get { return _chartItemCollection; }
            set
            {
                _chartItemCollection = value;
                OnPropertyChanged();
            }
        }

        private ChartItem _selectedChartItem;
        /// <summary>
        /// 選択されたグラフ アイテム
        /// </summary>
        public ChartItem SelectedChartItem
        {
            get { return _selectedChartItem; }
            set
            {
                _selectedChartItem = value;
                if (_selectedChartItem != null)
                {
                    SelectFeature(_selectedChartItem.ObjectId);
                }
                OnPropertyChanged();
            }
        }
        #endregion

        #region コマンド
        private RelayCommand _setTargetFieldCollectionCommand;
        /// <summary>
        /// 処理対象フィールド候補リストを設定するコマンド
        /// </summary>
        public RelayCommand SetTargetFieldCollectionCommand { get { return _setTargetFieldCollectionCommand; } }

        private RelayCommand _setTargetFiledCommand;
        /// <summary>
        /// 処理対象フィールドを設定するコマンド
        /// </summary>
        public RelayCommand SetTargetFiledCommand { get { return _setTargetFiledCommand; } }

        private RelayCommand _updateFilterCommand;
        /// <summary>
        /// フィルタを適用するコマンド
        /// </summary>
        public RelayCommand UpdateFilterCommand { get { return _updateFilterCommand; } }

        private RelayCommand _enableSAAnalysisCommand;
        /// <summary>
        /// 到達圏解析を有効化するコマンド
        /// </summary>
        public RelayCommand EnableSAAnalysisCommand { get { return _enableSAAnalysisCommand; } }

        private RelayCommand<MapViewInputEventArgs> _executeSAAnalysisCommand;
        /// <summary>
        /// 到達圏解析を実行するコマンド
        /// </summary>
        public RelayCommand<MapViewInputEventArgs> ExecuteSAAnalysisCommand { get { return _executeSAAnalysisCommand; } }
        #endregion

        #region Web マップの読み込みおよびマップ設定の制御
        /// <summary>
        /// Web マップの読み込み
        /// </summary>
        private async Task LoadWebMapAsync()
        {
            try
            {
                //処理開始
                this.IsBusy = true;

                //Web マップを読み込み
                if (_portal == null)
                    _portal = await ArcGISPortal.CreateAsync();
                var webmap = await WebMap.FromPortalItemAsync(this.LoadedPortalItem);
                this.CurrentWebMapVM = await WebMapViewModel.LoadAsync(webmap, _portal);

                //処理対象レイヤ候補リストを設定
                var targetLayers = CurrentWebMapVM.Map.Layers.Where(l => l is FeatureLayer);
                this.TargetLayerCollection = new ObservableCollection<Layer>(targetLayers);

                //マップ読み込み完了を通知
                IsMapReady = true;
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
        /// 処理対象フィールド候補を設定
        /// </summary>
        private void SetTargetFieldCollection()
        {
            //レイヤが選択されていなければ戻る
            if (this.TargetLayer == null)
            {
                this.IsTargetLayerSelected = false;
                return;
            }

            //ターゲットレイヤの OBJECTID フィールド名を取得
            var serviceFeatureTable = this.TargetLayer.FeatureTable as ServiceFeatureTable;
            _oidFieldName = serviceFeatureTable.ObjectIDField;

            //処理対象レイヤ選択完了を通知
            IsTargetLayerSelected = true;

            //数値型フィールドのみを取得
            var fieldInfos = this.TargetLayer.FeatureTable.Schema.Fields;
            var numFiedls = fieldInfos.Where(fi => fi.DataType == typeof(Int16)
                                                   || fi.DataType == typeof(Int32)
                                                   || fi.DataType == typeof(double));

            //処理対象フィールド候補リストを設定
            TargetFieldCollection = new ObservableCollection<Esri.ArcGISRuntime.Data.FieldInfo>(numFiedls);
        }

        /// <summary>
        /// 処理対象フィールドを設定
        /// </summary>
        private async void SetTargetFiled()
        {
            this.IsTargetFieldSelected = false;

            //フィールドが選択されていなければ戻る
            if (this.TargetField == null)
            {
                return;
            }

            //選択されたフィールドの最大値と最小値を取得するためのクエリタスクを作成
            var serviceFeatureTable = this.TargetLayer.FeatureTable as ServiceFeatureTable;
            QueryTask queryTask = new QueryTask(new Uri(serviceFeatureTable.ServiceUri));

            //クエリタスクに使用するフィルタはレイヤ取得時のフィルタ条件を引き継ぐ（フィルタ条件がなければ "1=1" を使用）
            string where = string.IsNullOrEmpty(_originalFilter) ? "1=1" : _originalFilter;

            //選択されたフィールドの最小値と最大値を取得するための統計クエリを作成
            var query = new Esri.ArcGISRuntime.Tasks.Query.Query(where)
            {
                OutStatistics = new List<OutStatistic> 
                    { 
                        new OutStatistic() 
                        {
                            OnStatisticField = this.TargetField.Name,
                            OutStatisticFieldName = MAXVALFIELD,
                            StatisticType = StatisticType.Max
                        },
                        new OutStatistic() 
                        {
                            OnStatisticField = this.TargetField.Name,
                            OutStatisticFieldName = MINVALFIELD,
                            StatisticType = StatisticType.Min
                        },
                    }
            };

            try
            {
                //最大値および最小値の取得
                var result = await queryTask.ExecuteAsync(query);
                if (result.FeatureSet.Features != null && result.FeatureSet.Features.Count > 0)
                {
                    //結果を取得
                    var resultFeature = result.FeatureSet.Features.FirstOrDefault();

                    //最小値を取得
                    var minVal = Double.Parse(resultFeature.Attributes[MINVALFIELD].ToString());

                    //最大値を取得
                    var maxVal = Double.Parse(resultFeature.Attributes[MAXVALFIELD].ToString());

                    //フィルタの最大値、最小値を設定し、現在のフィルタ値を最大値（フィルタ無し）に設定
                    if (minVal > this.CurrentFilterValue)
                    {
                        this.MaxFilterValue = maxVal;
                        this.CurrentFilterValue = maxVal;
                        this.MinFilterValue = minVal;
                    }
                    else
                    {
                        this.MinFilterValue = minVal;
                        this.CurrentFilterValue = maxVal;
                        this.MaxFilterValue = maxVal;
                    }
                }
            }
            catch (Exception ex)
            {
                //エラー通知
                var _ = new MessageDialog(ex.Message).ShowAsync();
            }

            //処理対象フィールド選択完了通知
            this.IsTargetFieldSelected = true;
        }
        #endregion

        #region 属性フィルタ
        /// <summary>
        /// フィルタを更新
        /// </summary>
        private void UpdateFilter()
        {
            //現在のタイマーを停止
            if (_updateFilterTimer != null) _updateFilterTimer.Stop();

            //フィルタ更新タイマーを開始（フィルタ値の最終更新後 0.5 秒経過したら更新）
            _updateFilterTimer = new DispatcherTimer();
            _updateFilterTimer.Interval = new TimeSpan(0, 0, 0, 0, 500);
            _updateFilterTimer.Tick += _updateFilterTimer_Tick;
            _updateFilterTimer.Start();
        }

        /// <summary>
        /// フィルタ更新タイマー進行時
        /// </summary>
        private async void _updateFilterTimer_Tick(object sender, object e)
        {
            //タイマーを停止
            _updateFilterTimer.Stop();
            _updateFilterTimer.Tick -= _updateFilterTimer_Tick;

            //処理対象フィールドが選択されていなければ戻る
            if (!this.IsTargetFieldSelected) return;

            //フィーチャ テーブルを取得
            var serviceFeatureTable = this.TargetLayer.FeatureTable as ServiceFeatureTable;

            //フィルタ文字列を設定
            serviceFeatureTable.Where = GenerateWhereString();

            //フィーチャをリフレッシュ
            serviceFeatureTable.RefreshFeatures(false);

            //到達圏解析結果が存在する場合は空間検索結果を更新
            if (_serviceAreaGraphic != null)
            {
                //現在の空間検索結果を削除
                ClearSpatialSelection();

                //空間検索を実行
                await SelectFeatureWithIn(_serviceAreaGraphic);
            }
        }
        #endregion

        #region 到達圏解析
        /// <summary>
        /// 到達圏解析を有効化可能かを判定
        /// </summary>
        private bool CanEnableServiceAreaAnalysis()
        {
            if (this.IsTargetFieldSelected && !_isExecutingSAAnalysis)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// 到達圏解析を実行可能に設定
        /// </summary>
        private void EnableServiceAreaAnalysis()
        {
            _canExecuteSAAnalysis = true;
        }

        /// <summary>
        /// 到達圏解析を実行するコマンド
        /// </summary>
        private async Task ExecuteServiceAreaAnalysis(MapViewInputEventArgs e)
        {
            //到達圏解析が実行不可であれば戻る
            if (!_canExecuteSAAnalysis)
            {
                return;
            }
            else
            {
                _canExecuteSAAnalysis = false;
            }

            //処理開始
            this.IsExecutingSAAnalysis = true;

            //前回の結果を削除
            ClearSAAnalysisResult();

            try
            {
                //到達圏解析開始ポイントをマップに追加
                var startPoint = new Graphic(e.Location);
                _serviceAreaStartPointOverlay.Graphics.Add(startPoint);

                //到達圏解析用パラメーターの作成
                GPInputParameter parameter = new GPInputParameter();
                parameter.GPParameters.Add(new GPFeatureRecordSetLayer("facilities", e.Location));  //解析の中心点
                parameter.GPParameters.Add(new GPString("break_values", "5"));                      //到達圏の範囲（5分）
                parameter.GPParameters.Add(new GPString("env:outSR", "102100"));                    //結果の空間参照（Web メルカトル）
                parameter.GPParameters.Add(new GPString("travel_mode", "Driving"));                 //車で到達できる範囲を解析
                parameter.GPParameters.Add(new GPString("detailed_polygons", "true"));              //解析モード（詳細モードに設定）

                //到達圏の解析を開始
                GPJobInfo result = await _serviceAreaGp.SubmitJobAsync(parameter);

                //到達圏の解析結果が"成功"、"失敗"、"時間切れ"、"キャンセル"のいずれかになるまで
                //2秒ごとに ArcGIS Online にステータスを確認
                while (result.JobStatus != GPJobStatus.Succeeded
                       && result.JobStatus != GPJobStatus.Failed
                       && result.JobStatus != GPJobStatus.TimedOut
                       && result.JobStatus != GPJobStatus.Cancelled)
                {
                    result = await _serviceAreaGp.CheckJobStatusAsync(result.JobID);

                    await Task.Delay(2000);
                }

                //到達圏解析の結果が成功した場合は結果を表示
                if (result.JobStatus == GPJobStatus.Succeeded)
                {
                    //到達圏解析の結果を取得
                    GPParameter resultData = await _serviceAreaGp.GetResultDataAsync(result.JobID, "Service_Areas");

                    //到達圏解析結果レイヤのグラフィックを結果グラフィックとして取得
                    GPFeatureRecordSetLayer gpLayer = resultData as GPFeatureRecordSetLayer;
                    _serviceAreaGraphic = gpLayer.FeatureSet.Features[0] as Graphic;

                    //到達圏解析結果を Web マップに追加
                    _serviceAreaResultOverlay.Graphics.Add(_serviceAreaGraphic);

                    //到達圏解析結果表示用のグラフィック内にある物件を選択
                    await SelectFeatureWithIn(_serviceAreaGraphic);
                }
                else
                {
                    //エラー通知
                    var _ = new MessageDialog("到達圏の解析に失敗しました。").ShowAsync();
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
                this.IsExecutingSAAnalysis = false;
            }
        }

        /// <summary>
        /// 到達圏解析の結果をクリア
        /// </summary>
        private void ClearSAAnalysisResult()
        {
            //空間検索の結果を削除
            ClearSpatialSelection();

            //グラフをクリア
            this.ChartItemCollection = null;

            //到達圏解析結果グラフィックを削除
            _serviceAreaGraphic = null;
            _serviceAreaResultOverlay.Graphics.Clear();
            _serviceAreaStartPointOverlay.Graphics.Clear();
        }
        #endregion

        #region 空間検索
        /// <summary>
        /// 到達圏エリア内にあるフィーチャを選択
        /// </summary>
        private async Task SelectFeatureWithIn(Graphic serviceAreaGraphic)
        {
            //到達圏解析の結果が存在しなければ戻る
            if (serviceAreaGraphic == null) return;

            try
            {
                //フィーチャテーブルを取得
                var serviceFeatureTable = this.TargetLayer.FeatureTable as ServiceFeatureTable;

                //空間検索を実行するためのクエリタスクを作成
                var queryTask = new Esri.ArcGISRuntime.Tasks.Query.QueryTask(new Uri(serviceFeatureTable.ServiceUri));

                //クエリタスクの検索条件を作成（既存のフィルタ条件を引き継ぐ）
                var query = new Esri.ArcGISRuntime.Tasks.Query.Query(GenerateWhereString());
                query.OutFields.Add("*");                                       //すべてのフィールドを取得する
                query.ReturnGeometry = true;                                    //ジオメトリを取得する
                query.Geometry = serviceAreaGraphic.Geometry;                   //到達圏解析の結果で空間検索を行う
                query.SpatialRelationship = SpatialRelationship.Intersects;     //空間検索の判定条件はインターセクトを使用する

                //空間検索を実行
                var queryResult = await queryTask.ExecuteAsync(query);

                //空間検索結果のフィーチャをマップに表示しグラフに追加
                var chartItemCollection = new List<ChartItem>();
                foreach (Feature f in queryResult.FeatureSet.Features)
                {
                    Graphic g = new Graphic(f.Geometry, f.Attributes);
                    _selectedFeatureOverlay.Graphics.Add(g);

                    var oid = (Int32)f.Attributes[_oidFieldName];
                    double itemValue;
                    if (double.TryParse(f.Attributes[this.TargetField.Name].ToString(), out itemValue))
                    {
                        var chartItem = new ChartItem()
                        {
                            ObjectId = oid,
                            ItemValue = itemValue,
                        };
                        chartItemCollection.Add(chartItem);
                    }
                }
                this.ChartItemCollection = new ObservableCollection<ChartItem>(chartItemCollection.OrderBy(ci => ci.ItemValue));
            }
            catch (Exception ex)
            {
                //エラー通知
                var _ = new MessageDialog(ex.Message).ShowAsync();
            }
        }

        /// <summary>
        /// 空間検索の結果を削除
        /// </summary>
        private void ClearSpatialSelection()
        {
            //選択フォーチャオーバーレイのグラフィックを削除
            _selectedFeatureOverlay.Graphics.Clear();
        }
        #endregion

        /// <summary>
        /// フィルタ文字列の作成
        /// </summary>
        private string GenerateWhereString()
        {
            string where;

            //処理対象レイヤの条件フィルタが存在する場合は、条件フィルタ + 処理対象フィールドのフィルタを使用
            if (!string.IsNullOrEmpty(_originalFilter))
            {
                where = string.Format("{0} AND ({1} <= {2})", _originalFilter, this.TargetField.Name, this.CurrentFilterValue.ToString());
            }
            //処理対象レイヤの条件フィルタが存在しない場合は処理対象フィールドのフィルタを使用
            else
            {
                where = string.Format("{0} <= {1}", this.TargetField.Name, this.CurrentFilterValue.ToString());
            }

            return where;
        }

        /// <summary>
        /// フィーチャの選択
        /// </summary>
        private void SelectFeature(long objectId)
        {
            //選択フォーチャオーバーレイの選択を解除
            _selectedFeatureOverlay.ClearSelection();

            //選択された OBJECTID のグラフィックを取得し選択
            var selectedGraphic = _selectedFeatureOverlay.Graphics.Where(g => objectId == ((Int32)g.Attributes[_oidFieldName])).FirstOrDefault();
            if (selectedGraphic != null)
            {
                selectedGraphic.IsSelected = true;
            };
        }
    }
}
