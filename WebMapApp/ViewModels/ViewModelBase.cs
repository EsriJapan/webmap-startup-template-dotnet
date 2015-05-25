using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WebMapApp.ViewModels
{
    /// <summary>
    /// ビューモデル ベースクラス
    /// </summary>
    public class ViewModelBase : INotifyPropertyChanged
    {
        #region INotifyPropertyChanged の実装
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName]string propertyName = null)
        {
            var h = this.PropertyChanged;
            if (h != null)
            {
                h(this, new PropertyChangedEventArgs(propertyName));
            }
        }
        #endregion

        private bool _isBusy;
        /// <summary>
        /// 処理中であることを示すフラグ
        /// </summary>
        public bool IsBusy
        {
            get { return _isBusy; }
            set
            {
                _isBusy = value;
                OnPropertyChanged();
            }
        }
    }
}
