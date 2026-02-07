package com.pronova.handlowiec.api

import com.pronova.handlowiec.data.model.*
import retrofit2.Response
import retrofit2.http.*

interface ApiService {

    // Authentication
    @POST("api/auth/login")
    suspend fun login(
        @Body request: LoginRequest
    ): Response<LoginResponse>

    // Orders
    @GET("api/zamowienia")
    suspend fun getOrders(
        @Query("data") date: String
    ): Response<List<Order>>

    @GET("api/zamowienia/{id}")
    suspend fun getOrder(
        @Path("id") id: Int
    ): Response<Order>

    @POST("api/zamowienia")
    suspend fun createOrder(
        @Body request: OrderCreateRequest
    ): Response<Order>

    @PUT("api/zamowienia/{id}")
    suspend fun updateOrder(
        @Path("id") id: Int,
        @Body request: OrderCreateRequest
    ): Response<Order>

    @PUT("api/zamowienia/{id}/anuluj")
    suspend fun cancelOrder(
        @Path("id") id: Int
    ): Response<Unit>

    // Customers
    @GET("api/klienci")
    suspend fun getCustomers(): Response<List<Customer>>

    @GET("api/klienci/{id}")
    suspend fun getCustomer(
        @Path("id") id: Int
    ): Response<Customer>

    // Products
    @GET("api/produkty")
    suspend fun getProducts(): Response<List<Product>>

    // Dashboard
    @GET("api/dashboard")
    suspend fun getDashboard(
        @Query("data") date: String
    ): Response<DashboardData>
}
