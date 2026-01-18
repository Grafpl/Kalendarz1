using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ZPSP.Sales.Infrastructure;
using ZPSP.Sales.Models;
using ZPSP.Sales.Services.Interfaces;

namespace ZPSP.Sales.ViewModels
{
    /// <summary>
    /// ViewModel dla głównego okna zamówień.
    /// Zarządza listą zamówień, filtrowaniem, agregacjami i interakcją z użytkownikiem.
    /// </summary>
    public class MainViewModel : BaseViewModel
    {
        private readonly IOrderService _orderService;
        private readonly IProductService _productService;
        private readonly ICacheService _cacheService;

        #region Private Fields

        private DateTime _selectedDate = DateTime.Today;
        private Order _selectedOrder;
        private string _filterText;
        private int? _selectedProductId;
        private bool _showCancelled = false;
        private bool _showReleasesWithoutOrders = true;
        private bool _useReleasesForBalance = false;
        private DashboardData _dashboardData;
        private string _userId;

        #endregion

        #region Constructor

        public MainViewModel(
            IOrderService orderService,
            IProductService productService,
            ICacheService cacheService)
        {
            _orderService = orderService ?? throw new ArgumentNullException(nameof(orderService));
            _productService = productService ?? throw new ArgumentNullException(nameof(productService));
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));

            // Inicjalizacja kolekcji
            Orders = new ObservableCollection<Order>();
            ProductAggregations = new ObservableCollection<ProductAggregation>();
            Products = new ObservableCollection<Product>();
            OrderItems = new ObservableCollection<OrderItem>();

            // Inicjalizacja komend
            InitializeCommands();
        }

        #endregion

        #region Observable Collections

        /// <summary>
        /// Lista zamówień na wybrany dzień
        /// </summary>
        public ObservableCollection<Order> Orders { get; }

        /// <summary>
        /// Agregacje produktów (bilansowanie)
        /// </summary>
        public ObservableCollection<ProductAggregation> ProductAggregations { get; }

        /// <summary>
        /// Lista produktów do filtrowania
        /// </summary>
        public ObservableCollection<Product> Products { get; }

        /// <summary>
        /// Pozycje wybranego zamówienia
        /// </summary>
        public ObservableCollection<OrderItem> OrderItems { get; }

        #endregion

        #region Properties

        /// <summary>
        /// Wybrana data
        /// </summary>
        public DateTime SelectedDate
        {
            get => _selectedDate;
            set
            {
                if (SetProperty(ref _selectedDate, value))
                {
                    _ = RefreshAllAsync();
                }
            }
        }

        /// <summary>
        /// Wybrane zamówienie
        /// </summary>
        public Order SelectedOrder
        {
            get => _selectedOrder;
            set
            {
                if (SetProperty(ref _selectedOrder, value))
                {
                    _ = LoadOrderDetailsAsync();
                    OnPropertyChanged(nameof(CanEditSelectedOrder));
                    OnPropertyChanged(nameof(HasSelectedOrder));
                }
            }
        }

        /// <summary>
        /// Tekst filtra odbiorcy
        /// </summary>
        public string FilterText
        {
            get => _filterText;
            set
            {
                if (SetProperty(ref _filterText, value))
                {
                    ApplyFilter();
                }
            }
        }

        /// <summary>
        /// Wybrany produkt do filtrowania
        /// </summary>
        public int? SelectedProductId
        {
            get => _selectedProductId;
            set
            {
                if (SetProperty(ref _selectedProductId, value))
                {
                    _ = RefreshOrdersAsync();
                }
            }
        }

        /// <summary>
        /// Czy pokazywać anulowane zamówienia
        /// </summary>
        public bool ShowCancelled
        {
            get => _showCancelled;
            set
            {
                if (SetProperty(ref _showCancelled, value))
                {
                    ApplyFilter();
                }
            }
        }

        /// <summary>
        /// Czy pokazywać wydania bez zamówień
        /// </summary>
        public bool ShowReleasesWithoutOrders
        {
            get => _showReleasesWithoutOrders;
            set
            {
                if (SetProperty(ref _showReleasesWithoutOrders, value))
                {
                    ApplyFilter();
                }
            }
        }

        /// <summary>
        /// Czy używać wydań (true) czy zamówień (false) do bilansu
        /// </summary>
        public bool UseReleasesForBalance
        {
            get => _useReleasesForBalance;
            set
            {
                if (SetProperty(ref _useReleasesForBalance, value))
                {
                    _ = RefreshAggregationsAsync();
                }
            }
        }

        /// <summary>
        /// Dane dashboardu
        /// </summary>
        public DashboardData DashboardData
        {
            get => _dashboardData;
            set => SetProperty(ref _dashboardData, value);
        }

        /// <summary>
        /// ID zalogowanego użytkownika
        /// </summary>
        public string UserId
        {
            get => _userId;
            set => SetProperty(ref _userId, value);
        }

        #endregion

        #region Calculated Properties

        /// <summary>
        /// Czy jest wybrane zamówienie
        /// </summary>
        public bool HasSelectedOrder => SelectedOrder != null;

        /// <summary>
        /// Czy wybrane zamówienie można edytować
        /// </summary>
        public bool CanEditSelectedOrder => SelectedOrder != null && _orderService.CanEditOrder(SelectedOrder);

        /// <summary>
        /// Suma zamówień
        /// </summary>
        public decimal TotalOrders => Orders?.Sum(o => o.IloscZamowiona) ?? 0;

        /// <summary>
        /// Suma wydań
        /// </summary>
        public decimal TotalReleases => Orders?.Sum(o => o.IloscFaktyczna) ?? 0;

        /// <summary>
        /// Liczba zamówień (bez anulowanych)
        /// </summary>
        public int OrderCount => Orders?.Count(o => o.Status != "Anulowane") ?? 0;

        /// <summary>
        /// Liczba klientów
        /// </summary>
        public int CustomerCount => Orders?.Select(o => o.KlientId).Distinct().Count() ?? 0;

        /// <summary>
        /// Nazwa wybranego dnia po polsku
        /// </summary>
        public string SelectedDayName => SelectedDate.ToString("dddd, d MMMM yyyy", new System.Globalization.CultureInfo("pl-PL"));

        /// <summary>
        /// Uwagi wybranego zamówienia
        /// </summary>
        public string SelectedOrderNotes
        {
            get => SelectedOrder?.Uwagi;
            set
            {
                if (SelectedOrder != null && SelectedOrder.Uwagi != value)
                {
                    _ = UpdateOrderNotesAsync(value);
                }
            }
        }

        #endregion

        #region Commands

        public ICommand RefreshCommand { get; private set; }
        public ICommand PreviousDayCommand { get; private set; }
        public ICommand NextDayCommand { get; private set; }
        public ICommand TodayCommand { get; private set; }
        public ICommand CancelOrderCommand { get; private set; }
        public ICommand RestoreOrderCommand { get; private set; }
        public ICommand DuplicateOrderCommand { get; private set; }
        public ICommand ClearFilterCommand { get; private set; }
        public ICommand ClearProductFilterCommand { get; private set; }
        public ICommand FilterByProductCommand { get; private set; }

        private void InitializeCommands()
        {
            RefreshCommand = new AsyncRelayCommand(RefreshAllAsync);
            PreviousDayCommand = new RelayCommand(() => SelectedDate = SelectedDate.AddDays(-1));
            NextDayCommand = new RelayCommand(() => SelectedDate = SelectedDate.AddDays(1));
            TodayCommand = new RelayCommand(() => SelectedDate = DateTime.Today);
            CancelOrderCommand = new AsyncRelayCommand<Order>(CancelOrderAsync, o => o != null && o.Status != "Anulowane");
            RestoreOrderCommand = new AsyncRelayCommand<Order>(RestoreOrderAsync, o => o != null && o.Status == "Anulowane");
            DuplicateOrderCommand = new AsyncRelayCommand<Order>(DuplicateOrderAsync, o => o != null);
            ClearFilterCommand = new RelayCommand(() => FilterText = "");
            ClearProductFilterCommand = new RelayCommand(() => SelectedProductId = null);
            FilterByProductCommand = new RelayCommand<int>(id => SelectedProductId = id);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Inicjalizuje ViewModel i ładuje dane
        /// </summary>
        public async Task InitializeAsync()
        {
            await ExecuteAsync(async () =>
            {
                // Załaduj produkty
                var products = await _productService.GetAllProductsAsync();
                Products.Clear();
                foreach (var product in products)
                {
                    Products.Add(product);
                }

                // Załaduj dane dla wybranej daty
                await RefreshAllAsync();

            }, "Inicjalizacja...");
        }

        /// <summary>
        /// Odświeża wszystkie dane
        /// </summary>
        public async Task RefreshAllAsync()
        {
            await ExecuteAsync(async () =>
            {
                await Task.WhenAll(
                    RefreshOrdersAsync(),
                    RefreshAggregationsAsync(),
                    RefreshDashboardAsync()
                );
            }, "Odświeżanie danych...");
        }

        /// <summary>
        /// Odświeża listę zamówień
        /// </summary>
        public async Task RefreshOrdersAsync()
        {
            var orders = await _orderService.GetOrdersForDateAsync(
                SelectedDate,
                ShowCancelled,
                SelectedProductId);

            Orders.Clear();
            foreach (var order in orders)
            {
                Orders.Add(order);
            }

            ApplyFilter();

            OnPropertyChanged(nameof(TotalOrders));
            OnPropertyChanged(nameof(TotalReleases));
            OnPropertyChanged(nameof(OrderCount));
            OnPropertyChanged(nameof(CustomerCount));
        }

        /// <summary>
        /// Odświeża agregacje produktów
        /// </summary>
        public async Task RefreshAggregationsAsync()
        {
            var aggregations = await _productService.GetProductAggregationsAsync(SelectedDate, UseReleasesForBalance);

            ProductAggregations.Clear();
            foreach (var agg in aggregations)
            {
                ProductAggregations.Add(agg);
            }
        }

        /// <summary>
        /// Odświeża dane dashboardu
        /// </summary>
        public async Task RefreshDashboardAsync()
        {
            DashboardData = await _orderService.GetDashboardDataAsync(SelectedDate);
        }

        #endregion

        #region Private Methods

        private async Task LoadOrderDetailsAsync()
        {
            if (SelectedOrder == null)
            {
                OrderItems.Clear();
                return;
            }

            var items = await _orderService.GetOrderItemsWithNamesAsync(SelectedOrder.Id);
            OrderItems.Clear();
            foreach (var item in items)
            {
                OrderItems.Add(item);
            }

            OnPropertyChanged(nameof(SelectedOrderNotes));
        }

        private void ApplyFilter()
        {
            // Implementacja filtrowania w ObservableCollection
            // W pełnej implementacji użylibyśmy CollectionView
            OnPropertyChanged(nameof(Orders));
        }

        private async Task CancelOrderAsync(Order order)
        {
            if (order == null) return;

            await ExecuteAsync(async () =>
            {
                await _orderService.CancelOrderAsync(order.Id, UserId, "Anulowano przez użytkownika");
                await RefreshOrdersAsync();
            }, "Anulowanie zamówienia...");
        }

        private async Task RestoreOrderAsync(Order order)
        {
            if (order == null) return;

            await ExecuteAsync(async () =>
            {
                await _orderService.RestoreOrderAsync(order.Id, UserId);
                await RefreshOrdersAsync();
            }, "Przywracanie zamówienia...");
        }

        private async Task DuplicateOrderAsync(Order order)
        {
            if (order == null) return;

            await ExecuteAsync(async () =>
            {
                // Domyślnie duplikuj na jutro
                var targetDate = SelectedDate.AddDays(1);
                await _orderService.DuplicateOrderAsync(order.Id, targetDate, false);
                StatusMessage = $"Zduplikowano zamówienie na {targetDate:dd.MM.yyyy}";
            }, "Duplikowanie zamówienia...");
        }

        private async Task UpdateOrderNotesAsync(string notes)
        {
            if (SelectedOrder == null) return;

            try
            {
                await _orderService.UpdateNotesAsync(SelectedOrder.Id, notes, UserId);
                SelectedOrder.Uwagi = notes;
                SelectedOrder.MaNotatke = !string.IsNullOrEmpty(notes);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Błąd zapisywania uwag: {ex.Message}";
            }
        }

        #endregion

        #region IDisposable

        protected override void OnDispose()
        {
            Orders.Clear();
            ProductAggregations.Clear();
            Products.Clear();
            OrderItems.Clear();
            base.OnDispose();
        }

        #endregion
    }
}
