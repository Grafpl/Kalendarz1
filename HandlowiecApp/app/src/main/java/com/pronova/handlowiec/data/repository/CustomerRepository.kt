package com.pronova.handlowiec.data.repository

import com.pronova.handlowiec.api.ApiService
import com.pronova.handlowiec.data.model.Customer
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.flow
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class CustomerRepository @Inject constructor(
    private val apiService: ApiService
) {

    /**
     * Get all customers.
     */
    fun getCustomers(): Flow<ApiResult<List<Customer>>> = flow {
        emit(ApiResult.Loading)
        try {
            val response = apiService.getCustomers()
            if (response.isSuccessful && response.body() != null) {
                emit(ApiResult.Success(response.body()!!))
            } else {
                emit(ApiResult.Error("Blad pobierania klientow: ${response.code()}", response.code()))
            }
        } catch (e: Exception) {
            emit(ApiResult.Error("Blad polaczenia: ${e.localizedMessage ?: "Nieznany blad"}"))
        }
    }

    /**
     * Get single customer by ID.
     */
    fun getCustomer(id: Int): Flow<ApiResult<Customer>> = flow {
        emit(ApiResult.Loading)
        try {
            val response = apiService.getCustomer(id)
            if (response.isSuccessful && response.body() != null) {
                emit(ApiResult.Success(response.body()!!))
            } else {
                emit(ApiResult.Error("Blad pobierania klienta: ${response.code()}", response.code()))
            }
        } catch (e: Exception) {
            emit(ApiResult.Error("Blad polaczenia: ${e.localizedMessage ?: "Nieznany blad"}"))
        }
    }
}
