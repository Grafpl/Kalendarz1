package com.pronova.handlowiec.ui.orders

import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.pronova.handlowiec.data.model.Order
import com.pronova.handlowiec.data.repository.ApiResult
import com.pronova.handlowiec.data.repository.OrderRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch
import javax.inject.Inject

sealed class OrderDetailUiState {
    data object Loading : OrderDetailUiState()
    data class Success(val order: Order) : OrderDetailUiState()
    data class Error(val message: String) : OrderDetailUiState()
}

sealed class CancelOrderState {
    data object Idle : CancelOrderState()
    data object Loading : CancelOrderState()
    data object Success : CancelOrderState()
    data class Error(val message: String) : CancelOrderState()
}

@HiltViewModel
class OrderDetailViewModel @Inject constructor(
    private val orderRepository: OrderRepository,
    savedStateHandle: SavedStateHandle
) : ViewModel() {

    private val orderId: Int = savedStateHandle.get<Int>("orderId") ?: 0

    private val _uiState = MutableStateFlow<OrderDetailUiState>(OrderDetailUiState.Loading)
    val uiState: StateFlow<OrderDetailUiState> = _uiState.asStateFlow()

    private val _cancelState = MutableStateFlow<CancelOrderState>(CancelOrderState.Idle)
    val cancelState: StateFlow<CancelOrderState> = _cancelState.asStateFlow()

    init {
        loadOrder()
    }

    fun loadOrder() {
        viewModelScope.launch {
            orderRepository.getOrder(orderId).collect { result ->
                when (result) {
                    is ApiResult.Loading -> _uiState.value = OrderDetailUiState.Loading
                    is ApiResult.Success -> _uiState.value = OrderDetailUiState.Success(result.data)
                    is ApiResult.Error -> _uiState.value = OrderDetailUiState.Error(result.message)
                }
            }
        }
    }

    fun cancelOrder() {
        viewModelScope.launch {
            _cancelState.value = CancelOrderState.Loading
            when (val result = orderRepository.cancelOrder(orderId)) {
                is ApiResult.Success -> {
                    _cancelState.value = CancelOrderState.Success
                    loadOrder() // Reload to get updated status
                }
                is ApiResult.Error -> {
                    _cancelState.value = CancelOrderState.Error(result.message)
                }
                is ApiResult.Loading -> { /* no-op */ }
            }
        }
    }

    fun resetCancelState() {
        _cancelState.value = CancelOrderState.Idle
    }
}
