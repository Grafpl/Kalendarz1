package com.pronova.handlowiec.data.repository

import com.pronova.handlowiec.api.ApiService
import com.pronova.handlowiec.data.model.Product
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.flow
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class ProductRepository @Inject constructor(
    private val apiService: ApiService
) {

    /**
     * Get all products.
     */
    fun getProducts(): Flow<ApiResult<List<Product>>> = flow {
        emit(ApiResult.Loading)
        try {
            val response = apiService.getProducts()
            if (response.isSuccessful && response.body() != null) {
                emit(ApiResult.Success(response.body()!!))
            } else {
                emit(ApiResult.Error("Blad pobierania produktow: ${response.code()}", response.code()))
            }
        } catch (e: Exception) {
            emit(ApiResult.Error("Blad polaczenia: ${e.localizedMessage ?: "Nieznany blad"}"))
        }
    }
}
