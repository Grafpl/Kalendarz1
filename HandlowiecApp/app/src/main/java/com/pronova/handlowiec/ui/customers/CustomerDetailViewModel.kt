package com.pronova.handlowiec.ui.customers

import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.pronova.handlowiec.data.model.Customer
import com.pronova.handlowiec.data.repository.ApiResult
import com.pronova.handlowiec.data.repository.CustomerRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch
import javax.inject.Inject

sealed class CustomerDetailUiState {
    data object Loading : CustomerDetailUiState()
    data class Success(val customer: Customer) : CustomerDetailUiState()
    data class Error(val message: String) : CustomerDetailUiState()
}

@HiltViewModel
class CustomerDetailViewModel @Inject constructor(
    private val customerRepository: CustomerRepository,
    savedStateHandle: SavedStateHandle
) : ViewModel() {

    private val customerId: Int = savedStateHandle.get<Int>("customerId") ?: 0

    private val _uiState = MutableStateFlow<CustomerDetailUiState>(CustomerDetailUiState.Loading)
    val uiState: StateFlow<CustomerDetailUiState> = _uiState.asStateFlow()

    init {
        loadCustomer()
    }

    fun loadCustomer() {
        viewModelScope.launch {
            customerRepository.getCustomer(customerId).collect { result ->
                when (result) {
                    is ApiResult.Loading -> _uiState.value = CustomerDetailUiState.Loading
                    is ApiResult.Success -> _uiState.value = CustomerDetailUiState.Success(result.data)
                    is ApiResult.Error -> _uiState.value = CustomerDetailUiState.Error(result.message)
                }
            }
        }
    }
}
