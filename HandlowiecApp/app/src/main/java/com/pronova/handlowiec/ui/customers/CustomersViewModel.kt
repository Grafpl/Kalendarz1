package com.pronova.handlowiec.ui.customers

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.pronova.handlowiec.data.model.Customer
import com.pronova.handlowiec.data.repository.ApiResult
import com.pronova.handlowiec.data.repository.CustomerRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.combine
import kotlinx.coroutines.launch
import javax.inject.Inject

sealed class CustomersUiState {
    data object Loading : CustomersUiState()
    data class Success(val customers: List<Customer>) : CustomersUiState()
    data class Error(val message: String) : CustomersUiState()
}

@HiltViewModel
class CustomersViewModel @Inject constructor(
    private val customerRepository: CustomerRepository
) : ViewModel() {

    private val _uiState = MutableStateFlow<CustomersUiState>(CustomersUiState.Loading)
    val uiState: StateFlow<CustomersUiState> = _uiState.asStateFlow()

    private val _searchQuery = MutableStateFlow("")
    val searchQuery: StateFlow<String> = _searchQuery.asStateFlow()

    private val _isRefreshing = MutableStateFlow(false)
    val isRefreshing: StateFlow<Boolean> = _isRefreshing.asStateFlow()

    private val _allCustomers = MutableStateFlow<List<Customer>>(emptyList())

    init {
        loadCustomers()
        // Observe search query changes and filter
        viewModelScope.launch {
            combine(_allCustomers, _searchQuery) { customers, query ->
                if (query.isBlank()) customers
                else customers.filter { customer ->
                    customer.nazwa.contains(query, ignoreCase = true) ||
                    customer.shortcut.contains(query, ignoreCase = true) ||
                    customer.miasto.contains(query, ignoreCase = true) ||
                    customer.telefon.contains(query, ignoreCase = true) ||
                    customer.nip.contains(query, ignoreCase = true)
                }
            }.collect { filteredCustomers ->
                if (_uiState.value !is CustomersUiState.Loading && _uiState.value !is CustomersUiState.Error) {
                    _uiState.value = CustomersUiState.Success(filteredCustomers)
                }
            }
        }
    }

    fun onSearchQueryChange(query: String) {
        _searchQuery.value = query
    }

    fun refresh() {
        _isRefreshing.value = true
        loadCustomers()
    }

    fun loadCustomers() {
        viewModelScope.launch {
            customerRepository.getCustomers().collect { result ->
                when (result) {
                    is ApiResult.Loading -> {
                        if (!_isRefreshing.value) {
                            _uiState.value = CustomersUiState.Loading
                        }
                    }
                    is ApiResult.Success -> {
                        _allCustomers.value = result.data
                        _uiState.value = CustomersUiState.Success(result.data)
                        _isRefreshing.value = false
                    }
                    is ApiResult.Error -> {
                        _uiState.value = CustomersUiState.Error(result.message)
                        _isRefreshing.value = false
                    }
                }
            }
        }
    }
}
