package com.pronova.handlowiec.ui.orders

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.pronova.handlowiec.data.model.*
import com.pronova.handlowiec.data.repository.ApiResult
import com.pronova.handlowiec.data.repository.CustomerRepository
import com.pronova.handlowiec.data.repository.OrderRepository
import com.pronova.handlowiec.data.repository.ProductRepository
import com.pronova.handlowiec.util.DateUtils
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch
import java.time.LocalDate
import javax.inject.Inject

data class OrderFormItem(
    val id: Int = 0,
    val product: Product? = null,
    val quantity: String = "",
    val price: String = "",
    val folia: Boolean = false,
    val hallal: Boolean = false
)

sealed class OrderCreateUiState {
    data object Idle : OrderCreateUiState()
    data object Saving : OrderCreateUiState()
    data object Success : OrderCreateUiState()
    data class Error(val message: String) : OrderCreateUiState()
    data class ValidationError(val message: String) : OrderCreateUiState()
}

@HiltViewModel
class OrderCreateViewModel @Inject constructor(
    private val orderRepository: OrderRepository,
    private val customerRepository: CustomerRepository,
    private val productRepository: ProductRepository
) : ViewModel() {

    private val _uiState = MutableStateFlow<OrderCreateUiState>(OrderCreateUiState.Idle)
    val uiState: StateFlow<OrderCreateUiState> = _uiState.asStateFlow()

    private val _customers = MutableStateFlow<List<Customer>>(emptyList())
    val customers: StateFlow<List<Customer>> = _customers.asStateFlow()

    private val _products = MutableStateFlow<List<Product>>(emptyList())
    val products: StateFlow<List<Product>> = _products.asStateFlow()

    private val _selectedCustomer = MutableStateFlow<Customer?>(null)
    val selectedCustomer: StateFlow<Customer?> = _selectedCustomer.asStateFlow()

    private val _slaughterDate = MutableStateFlow(LocalDate.now())
    val slaughterDate: StateFlow<LocalDate> = _slaughterDate.asStateFlow()

    private val _acceptanceDate = MutableStateFlow(LocalDate.now())
    val acceptanceDate: StateFlow<LocalDate> = _acceptanceDate.asStateFlow()

    private val _notes = MutableStateFlow("")
    val notes: StateFlow<String> = _notes.asStateFlow()

    private val _currency = MutableStateFlow("PLN")
    val currency: StateFlow<String> = _currency.asStateFlow()

    private val _items = MutableStateFlow<List<OrderFormItem>>(listOf(OrderFormItem(id = 1)))
    val items: StateFlow<List<OrderFormItem>> = _items.asStateFlow()

    private var nextItemId = 2

    private val _isLoadingData = MutableStateFlow(true)
    val isLoadingData: StateFlow<Boolean> = _isLoadingData.asStateFlow()

    init {
        loadCustomersAndProducts()
    }

    private fun loadCustomersAndProducts() {
        viewModelScope.launch {
            _isLoadingData.value = true
            // Load customers
            launch {
                customerRepository.getCustomers().collect { result ->
                    if (result is ApiResult.Success) {
                        _customers.value = result.data.filter { it.aktywny }
                    }
                }
            }
            // Load products
            launch {
                productRepository.getProducts().collect { result ->
                    if (result is ApiResult.Success) {
                        _products.value = result.data.filter { it.aktywny }
                        _isLoadingData.value = false
                    }
                }
            }
        }
    }

    fun onCustomerSelected(customer: Customer) {
        _selectedCustomer.value = customer
        if (_uiState.value is OrderCreateUiState.ValidationError) {
            _uiState.value = OrderCreateUiState.Idle
        }
    }

    fun onSlaughterDateSelected(date: LocalDate) {
        _slaughterDate.value = date
    }

    fun onAcceptanceDateSelected(date: LocalDate) {
        _acceptanceDate.value = date
    }

    fun onNotesChange(value: String) {
        _notes.value = value
    }

    fun onCurrencyChange(value: String) {
        _currency.value = value
    }

    fun addItem() {
        val currentItems = _items.value.toMutableList()
        currentItems.add(OrderFormItem(id = nextItemId++))
        _items.value = currentItems
    }

    fun removeItem(itemId: Int) {
        val currentItems = _items.value.toMutableList()
        if (currentItems.size > 1) {
            currentItems.removeAll { it.id == itemId }
            _items.value = currentItems
        }
    }

    fun updateItem(itemId: Int, update: (OrderFormItem) -> OrderFormItem) {
        _items.value = _items.value.map { item ->
            if (item.id == itemId) update(item) else item
        }
    }

    fun saveOrder() {
        // Validate
        val customer = _selectedCustomer.value
        if (customer == null) {
            _uiState.value = OrderCreateUiState.ValidationError("Wybierz klienta")
            return
        }

        val validItems = _items.value.filter { it.product != null && it.quantity.isNotBlank() }
        if (validItems.isEmpty()) {
            _uiState.value = OrderCreateUiState.ValidationError("Dodaj przynajmniej jedna pozycje")
            return
        }

        val request = OrderCreateRequest(
            klientId = customer.id,
            dataUboju = DateUtils.formatForApi(_slaughterDate.value),
            dataPrzyjecia = DateUtils.formatForApi(_acceptanceDate.value),
            uwagi = _notes.value,
            waluta = _currency.value,
            pozycje = validItems.map { item ->
                OrderItemCreate(
                    kodTowaru = item.product!!.kod,
                    ilosc = item.quantity.toDoubleOrNull() ?: 0.0,
                    cena = item.price.toDoubleOrNull() ?: 0.0,
                    folia = item.folia,
                    hallal = item.hallal
                )
            }
        )

        viewModelScope.launch {
            _uiState.value = OrderCreateUiState.Saving
            when (val result = orderRepository.createOrder(request)) {
                is ApiResult.Success -> {
                    _uiState.value = OrderCreateUiState.Success
                }
                is ApiResult.Error -> {
                    _uiState.value = OrderCreateUiState.Error(result.message)
                }
                is ApiResult.Loading -> { /* no-op */ }
            }
        }
    }
}
