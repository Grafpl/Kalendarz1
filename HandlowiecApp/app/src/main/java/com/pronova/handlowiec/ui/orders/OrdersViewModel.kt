package com.pronova.handlowiec.ui.orders

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.pronova.handlowiec.data.model.Order
import com.pronova.handlowiec.data.repository.ApiResult
import com.pronova.handlowiec.data.repository.OrderRepository
import com.pronova.handlowiec.util.DateUtils
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.combine
import kotlinx.coroutines.launch
import java.time.LocalDate
import javax.inject.Inject

sealed class OrdersUiState {
    data object Loading : OrdersUiState()
    data class Success(val orders: List<Order>) : OrdersUiState()
    data class Error(val message: String) : OrdersUiState()
}

@HiltViewModel
class OrdersViewModel @Inject constructor(
    private val orderRepository: OrderRepository
) : ViewModel() {

    private val _uiState = MutableStateFlow<OrdersUiState>(OrdersUiState.Loading)
    val uiState: StateFlow<OrdersUiState> = _uiState.asStateFlow()

    private val _selectedDate = MutableStateFlow(LocalDate.now())
    val selectedDate: StateFlow<LocalDate> = _selectedDate.asStateFlow()

    private val _searchQuery = MutableStateFlow("")
    val searchQuery: StateFlow<String> = _searchQuery.asStateFlow()

    private val _isRefreshing = MutableStateFlow(false)
    val isRefreshing: StateFlow<Boolean> = _isRefreshing.asStateFlow()

    private val _allOrders = MutableStateFlow<List<Order>>(emptyList())

    init {
        loadOrders()
        // Observe search query changes and filter
        viewModelScope.launch {
            combine(_allOrders, _searchQuery) { orders, query ->
                if (query.isBlank()) orders
                else orders.filter { order ->
                    order.odbiorca.contains(query, ignoreCase = true) ||
                    order.handlowiec.contains(query, ignoreCase = true) ||
                    order.status.contains(query, ignoreCase = true) ||
                    order.uwagi.contains(query, ignoreCase = true)
                }
            }.collect { filteredOrders ->
                if (_uiState.value !is OrdersUiState.Loading && _uiState.value !is OrdersUiState.Error) {
                    _uiState.value = OrdersUiState.Success(filteredOrders)
                }
            }
        }
    }

    fun onDateSelected(date: LocalDate) {
        _selectedDate.value = date
        _searchQuery.value = ""
        loadOrders()
    }

    fun onSearchQueryChange(query: String) {
        _searchQuery.value = query
    }

    fun refresh() {
        _isRefreshing.value = true
        loadOrders()
    }

    fun loadOrders() {
        viewModelScope.launch {
            val dateStr = DateUtils.formatForApi(_selectedDate.value)
            orderRepository.getOrders(dateStr).collect { result ->
                when (result) {
                    is ApiResult.Loading -> {
                        if (!_isRefreshing.value) {
                            _uiState.value = OrdersUiState.Loading
                        }
                    }
                    is ApiResult.Success -> {
                        _allOrders.value = result.data
                        _uiState.value = OrdersUiState.Success(result.data)
                        _isRefreshing.value = false
                    }
                    is ApiResult.Error -> {
                        _uiState.value = OrdersUiState.Error(result.message)
                        _isRefreshing.value = false
                    }
                }
            }
        }
    }
}
