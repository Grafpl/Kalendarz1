package com.pronova.handlowiec.ui.dashboard

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.pronova.handlowiec.data.model.DashboardData
import com.pronova.handlowiec.data.repository.ApiResult
import com.pronova.handlowiec.data.repository.OrderRepository
import com.pronova.handlowiec.util.DateUtils
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch
import java.time.LocalDate
import javax.inject.Inject

sealed class DashboardUiState {
    data object Loading : DashboardUiState()
    data class Success(val data: DashboardData) : DashboardUiState()
    data class Error(val message: String) : DashboardUiState()
}

@HiltViewModel
class DashboardViewModel @Inject constructor(
    private val orderRepository: OrderRepository
) : ViewModel() {

    private val _uiState = MutableStateFlow<DashboardUiState>(DashboardUiState.Loading)
    val uiState: StateFlow<DashboardUiState> = _uiState.asStateFlow()

    private val _selectedDate = MutableStateFlow(LocalDate.now())
    val selectedDate: StateFlow<LocalDate> = _selectedDate.asStateFlow()

    private val _isRefreshing = MutableStateFlow(false)
    val isRefreshing: StateFlow<Boolean> = _isRefreshing.asStateFlow()

    init {
        loadDashboard()
    }

    fun onDateSelected(date: LocalDate) {
        _selectedDate.value = date
        loadDashboard()
    }

    fun refresh() {
        _isRefreshing.value = true
        loadDashboard()
    }

    fun loadDashboard() {
        viewModelScope.launch {
            val dateStr = DateUtils.formatForApi(_selectedDate.value)
            orderRepository.getDashboard(dateStr).collect { result ->
                when (result) {
                    is ApiResult.Loading -> {
                        if (!_isRefreshing.value) {
                            _uiState.value = DashboardUiState.Loading
                        }
                    }
                    is ApiResult.Success -> {
                        _uiState.value = DashboardUiState.Success(result.data)
                        _isRefreshing.value = false
                    }
                    is ApiResult.Error -> {
                        _uiState.value = DashboardUiState.Error(result.message)
                        _isRefreshing.value = false
                    }
                }
            }
        }
    }
}
