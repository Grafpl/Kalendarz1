package com.pronova.handlowiec.data.repository

import javax.inject.Inject
import javax.inject.Singleton

/**
 * In-memory token holder that can be accessed synchronously by the AuthInterceptor.
 * The token is persisted to DataStore by AuthRepository and loaded on app start.
 */
@Singleton
class TokenProvider @Inject constructor() {

    @Volatile
    private var token: String? = null

    fun getToken(): String? = token

    fun setToken(newToken: String?) {
        token = newToken
    }

    fun clearToken() {
        token = null
    }

    fun isLoggedIn(): Boolean = token != null
}
