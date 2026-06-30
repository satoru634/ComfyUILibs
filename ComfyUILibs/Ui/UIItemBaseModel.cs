using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace ComfyUILibs.Ui
{
    /// <summary>
    /// UI のアイテムリストを管理するための汎用モデル。
    /// </summary>
    /// <typeparam name="T">アイテムの型。</typeparam>
    public partial class UIItemBaseModel<T> : ObservableObject
    {
        /// <summary>
        /// アイテムリスト。UI で表示するアイテムのコレクションを保持する。
        /// </summary>
        public ObservableCollection<T> ItemList { get; set; }

        /// <summary>
        /// 選択されているアイテムのインデックス。-1 の場合は未選択を意味する。
        /// </summary>
        [ObservableProperty]
        private int _selectedIndex;

        /// <summary>
        /// コントロールが有効かどうかを示すフラグ。UI での操作可否を制御する。
        /// </summary>
        [ObservableProperty]
        private bool _enable;

        /// <summary>
        /// デフォルトコンストラクタ。アイテムリストを初期化し、選択インデックスを -1 に設定する。
        /// </summary>
        public UIItemBaseModel()
        {
            ItemList = new ObservableCollection<T>();
            _selectedIndex = -1;
            _enable = false;
        }

        /// <summary>
        /// コンストラクタ。既存の UIItemBaseModel<T> インスタンスからデータをコピーして初期化する。
        /// </summary>
        /// <param name="data">コピー元の UIItemBaseModel<T> インスタンス。</param>
        public UIItemBaseModel(UIItemBaseModel<T> data)
        {
            ItemList = data.ItemList;
            _selectedIndex = data.SelectedIndex;
            _enable = data.Enable;
        }

        /// <summary>
        /// 初期化メソッド。指定されたアイテムリストと選択オブジェクトで UIItemBaseModel<T> を初期化する。
        /// </summary>
        /// <param name="items">初期化に使用するアイテムリスト。</param>
        /// <param name="selectedObject">初期選択オブジェクト。指定されたオブジェクトがアイテムリストに存在しない場合、最初のアイテムが選択される。</param>
        public virtual void Init(List<T> items, T selectedObject)
        {
            if (ItemList.Count > 0)
            {
                Clear();
            }

            if (items.Count == 0)
            {
                SelectedIndex = -1;
                return;
            }

            foreach (var item in items)
            {
                ItemList.Add(item);
            }

            SelectedIndex = items.FindIndex(item => Equals(item, selectedObject));
            if (SelectedIndex == -1)
            {
                SelectedIndex = 0;
            }

            Enable = true;
            return;
        }

        /// <summary>
        /// 追加メソッド。指定されたアイテムをアイテムリストに追加し、必要に応じて選択状態を更新する。
        /// </summary>
        /// <param name="data">追加するアイテム。</param>
        /// <param name="selected">追加後に選択するかどうかを示すフラグ。true の場合、追加したアイテムが選択される。</param>
        public virtual void Add(T data, bool selected = false)
        {
            ItemList.Add(data);

            if (selected)
            {
                SelectedIndex = ItemList.IndexOf(data);
            }

            Enable = true;
            return;
        }

        /// <summary>
        /// アイテムリストをクリアし、選択インデックスを -1 にリセットし、コントロールを無効化する。
        /// </summary>
        public virtual void Clear()
        {
            ItemList.Clear();
            SelectedIndex = -1;
            Enable = false;
        }
    }
}
