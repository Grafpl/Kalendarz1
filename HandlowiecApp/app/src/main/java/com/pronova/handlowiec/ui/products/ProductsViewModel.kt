package com.pronova.handlowiec.ui.products

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.pronova.handlowiec.data.model.Product
import com.pronova.handlowiec.data.repository.ApiResult
import com.pronova.handlowiec.data.repository.ProductRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.combine
import kotlinx.coroutines.launch
import javax.inject.Inject

sealed class ProductsUiState {
    data object Loading : ProductsUiState()
    data class Success(val productsByCategory: Map<String, List<Product>>) : ProductsUiState()
    data class Error(val message: String) : ProductsUiState()
}

@HiltViewModel
class ProductsViewModel @Inject constructor(
    private val productRepository: ProductRepository
) : ViewModel() {

    private val _uiState = MutableStateFlow<ProductsUiState>(ProductsUiState.Loading)
    val uiState: StateFlow<ProductsUiState> = _uiState.asStateFlow()

    private val _searchQuery = MutableStateFlow("")
    val searchQuery: StateFlow<String> = _searchQuery.asStateFlow()

    private val _isRefreshing = MutableStateFlow(false)
    val isRefreshing: StateFlow<Boolean> = _isRefreshing.asStateFlow()

    private val _allProducts = MutableStateFlow<List<Product>>(emptyList())

    init {
        loadProducts()
        // Observe search query changes and filter
        viewModelScope.launch {
            combine(_allProducts, _searchQuery) { products, query ->
                val filtered = if (query.isBlank()) products
                else products.filter { product ->
                    product.nazwa.contains(query, ignoreCase = true) ||
                    product.kod.contains(query, ignoreCase = true) ||
                    product.katalog.contains(query, ignoreCase = true)
                }
                filtered.groupBy { it.katalog.ifBlank { "Inne" } }
                    .toSortedMap()
            }.collect { groupedProducts ->
                if (_uiState.value !is ProductsUiState.Loading && _uiState.value !is ProductsUiState.Error) {
                    _uiState.value = ProductsUiState.Success(groupedProducts)
                }
            }
        }
    }

    fun onSearchQueryChange(query: String) {
        _searchQuery.value = query
    }

    fun refresh() {
        _isRefreshing.value = true
        loadProducts()
    }

    fun loadProducts() {
        viewModelScope.launch {
            productRepository.getProducts().collect { result ->
                when (result) {
                    is ApiResult.Loading -> {
                        if (!_isRefreshing.value) {
                            _uiState.value = ProductsUiState.Loading
                        }
                    }
                    is ApiResult.Success -> {
                        _allProducts.value = result.data
                        val grouped = result.data
                            .groupBy { it.katalog.ifBlank { "Inne" } }
                            .toSortedMap()
                        _uiState.value = ProductsUiState.Success(grouped)
                        _isRefreshing.value = false
                    }
                    is ApiResult.Error -> {
                        _uiState.value = ProductsUiState.Error(result.message)
                        _isRefreshing.value = false
                    }
                }
            }
        }
    }
}
