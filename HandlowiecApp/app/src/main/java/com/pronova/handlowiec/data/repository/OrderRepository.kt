package com.pronova.handlowiec.data.repository

import com.pronova.handlowiec.api.ApiService
import com.pronova.handlowiec.data.model.DashboardData
import com.pronova.handlowiec.data.model.Order
import com.pronova.handlowiec.data.model.OrderCreateRequest
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.flow
import javax.inject.Inject
import javax.inject.Singleton

sealed class ApiResult<out T> {
    data class Success<T>(val data: T) : ApiResult<T>()
    data class Error(val message: String, val code: Int = 0) : ApiResult<Nothing>()
    data object Loading : ApiResult<Nothing>()
}

@Singleton
class OrderRepository @Inject constructor(
    private val apiService: ApiService
) {

    /**
     * Get orders for a specific date.
     */
    fun getOrders(date: String): Flow<ApiResult<List<Order>>> = flow {
        emit(ApiResult.Loading)
        try {
            val response = apiService.getOrders(date)
            if (response.isSuccessful && response.body() != null) {
                emit(ApiResult.Success(response.body()!!))
            } else {
                emit(ApiResult.Error("Blad pobierania zamowien: ${response.code()}", response.code()))
            }
        } catch (e: Exception) {
            emit(ApiResult.Error("Blad polaczenia: ${e.localizedMessage ?: "Nieznany blad"}"))
        }
    }

    /**
     * Get single order by ID.
     */
    fun getOrder(id: Int): Flow<ApiResult<Order>> = flow {
        emit(ApiResult.Loading)
        try {
            val response = apiService.getOrder(id)
            if (response.isSuccessful && response.body() != null) {
                emit(ApiResult.Success(response.body()!!))
            } else {
                emit(ApiResult.Error("Blad pobierania zamowienia: ${response.code()}", response.code()))
            }
        } catch (e: Exception) {
            emit(ApiResult.Error("Blad polaczenia: ${e.localizedMessage ?: "Nieznany blad"}"))
        }
    }

    /**
     * Create a new order.
     */
    suspend fun createOrder(request: OrderCreateRequest): ApiResult<Order> {
        return try {
            val response = apiService.createOrder(request)
            if (response.isSuccessful && response.body() != null) {
                ApiResult.Success(response.body()!!)
            } else {
                ApiResult.Error("Blad tworzenia zamowienia: ${response.code()}", response.code())
            }
        } catch (e: Exception) {
            ApiResult.Error("Blad polaczenia: ${e.localizedMessage ?: "Nieznany blad"}")
        }
    }

    /**
     * Update an existing order.
     */
    suspend fun updateOrder(id: Int, request: OrderCreateRequest): ApiResult<Order> {
        return try {
            val response = apiService.updateOrder(id, request)
            if (response.isSuccessful && response.body() != null) {
                ApiResult.Success(response.body()!!)
            } else {
                ApiResult.Error("Blad aktualizacji zamowienia: ${response.code()}", response.code())
            }
        } catch (e: Exception) {
            ApiResult.Error("Blad polaczenia: ${e.localizedMessage ?: "Nieznany blad"}")
        }
    }

    /**
     * Cancel an order.
     */
    suspend fun cancelOrder(id: Int): ApiResult<Unit> {
        return try {
            val response = apiService.cancelOrder(id)
            if (response.isSuccessful) {
                ApiResult.Success(Unit)
            } else {
                ApiResult.Error("Blad anulowania zamowienia: ${response.code()}", response.code())
            }
        } catch (e: Exception) {
            ApiResult.Error("Blad polaczenia: ${e.localizedMessage ?: "Nieznany blad"}")
        }
    }

    /**
     * Get dashboard data for a specific date.
     */
    fun getDashboard(date: String): Flow<ApiResult<DashboardData>> = flow {
        emit(ApiResult.Loading)
        try {
            val response = apiService.getDashboard(date)
            if (response.isSuccessful && response.body() != null) {
                emit(ApiResult.Success(response.body()!!))
            } else {
                emit(ApiResult.Error("Blad pobierania danych: ${response.code()}", response.code()))
            }
        } catch (e: Exception) {
            emit(ApiResult.Error("Blad polaczenia: ${e.localizedMessage ?: "Nieznany blad"}"))
        }
    }
}
