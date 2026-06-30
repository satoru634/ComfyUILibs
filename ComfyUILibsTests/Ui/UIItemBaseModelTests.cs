using ComfyUILibs.Ui;

namespace ComfyUILibsTests.Ui
{
    public class UIItemBaseModelTests
    {
        // ── コンストラクター ───────────────────────────────────────────────────

        [Fact]
        public void Constructor_Default_ItemListIsEmpty()
        {
            var model = new UIItemBaseModel<string>();
            Assert.Empty(model.ItemList);
        }

        [Fact]
        public void Constructor_Default_SelectedIndexIsMinusOne()
        {
            var model = new UIItemBaseModel<string>();
            Assert.Equal(-1, model.SelectedIndex);
        }

        [Fact]
        public void Constructor_Default_EnableIsFalse()
        {
            var model = new UIItemBaseModel<string>();
            Assert.False(model.Enable);
        }

        [Fact]
        public void Constructor_CopyFromExisting_CopiesState()
        {
            var source = new UIItemBaseModel<string>();
            source.Init(new List<string> { "a", "b" }, "b");

            var copy = new UIItemBaseModel<string>(source);

            Assert.Same(source.ItemList, copy.ItemList);
            Assert.Equal(source.SelectedIndex, copy.SelectedIndex);
            Assert.Equal(source.Enable, copy.Enable);
        }

        // ── Init ──────────────────────────────────────────────────────────────

        [Fact]
        public void Init_WithItems_PopulatesItemList()
        {
            var model = new UIItemBaseModel<string>();

            model.Init(new List<string> { "a", "b", "c" }, "b");

            Assert.Equal(new[] { "a", "b", "c" }, model.ItemList);
        }

        [Fact]
        public void Init_WithExistingSelectedObject_SelectsMatchingIndex()
        {
            var model = new UIItemBaseModel<string>();

            model.Init(new List<string> { "a", "b", "c" }, "b");

            Assert.Equal(1, model.SelectedIndex);
        }

        [Fact]
        public void Init_WithUnknownSelectedObject_SelectsFirstIndex()
        {
            var model = new UIItemBaseModel<string>();

            model.Init(new List<string> { "a", "b", "c" }, "unknown");

            Assert.Equal(0, model.SelectedIndex);
        }

        [Fact]
        public void Init_WithEmptyList_SelectedIndexIsMinusOne()
        {
            var model = new UIItemBaseModel<string>();

            model.Init(new List<string>(), "unused");

            Assert.Equal(-1, model.SelectedIndex);
        }

        [Fact]
        public void Init_WithItems_SetsEnableTrue()
        {
            var model = new UIItemBaseModel<string>();

            model.Init(new List<string> { "a" }, "a");

            Assert.True(model.Enable);
        }

        [Fact]
        public void Init_CalledTwice_ReplacesPreviousItemsWithoutAccumulating()
        {
            var model = new UIItemBaseModel<string>();
            model.Init(new List<string> { "a", "b" }, "a");

            model.Init(new List<string> { "x", "y", "z" }, "y");

            Assert.Equal(new[] { "x", "y", "z" }, model.ItemList);
            Assert.Equal(1, model.SelectedIndex);
        }

        // ── Add ───────────────────────────────────────────────────────────────

        [Fact]
        public void Add_AppendsItemToList()
        {
            var model = new UIItemBaseModel<string>();

            model.Add("a");
            model.Add("b");

            Assert.Equal(new[] { "a", "b" }, model.ItemList);
        }

        [Fact]
        public void Add_WithoutSelectedFlag_DoesNotChangeSelectedIndex()
        {
            var model = new UIItemBaseModel<string>();

            model.Add("a");

            Assert.Equal(-1, model.SelectedIndex);
        }

        [Fact]
        public void Add_WithSelectedTrue_SelectsAddedItem()
        {
            var model = new UIItemBaseModel<string>();
            model.Add("a");

            model.Add("b", selected: true);

            Assert.Equal(1, model.SelectedIndex);
        }

        [Fact]
        public void Add_SetsEnableTrue()
        {
            var model = new UIItemBaseModel<string>();

            model.Add("a");

            Assert.True(model.Enable);
        }

        // ── Clear ─────────────────────────────────────────────────────────────

        [Fact]
        public void Clear_EmptiesItemList()
        {
            var model = new UIItemBaseModel<string>();
            model.Init(new List<string> { "a", "b" }, "a");

            model.Clear();

            Assert.Empty(model.ItemList);
        }

        [Fact]
        public void Clear_ResetsSelectedIndexToMinusOne()
        {
            var model = new UIItemBaseModel<string>();
            model.Init(new List<string> { "a", "b" }, "a");

            model.Clear();

            Assert.Equal(-1, model.SelectedIndex);
        }

        [Fact]
        public void Clear_SetsEnableFalse()
        {
            var model = new UIItemBaseModel<string>();
            model.Init(new List<string> { "a", "b" }, "a");

            model.Clear();

            Assert.False(model.Enable);
        }
    }
}
