package com.pronova.handlowiec.data.repository

import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.Preferences
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.stringPreferencesKey
import com.pronova.handlowiec.api.ApiService
import com.pronova.handlowiec.data.model.LoginRequest
import com.pronova.handlowiec.data.model.LoginResponse
import com.pronova.handlowiec.util.Constants
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.map
import javax.inject.Inject
import javax.inject.Singleton

sealed class AuthResult {
    data class Success(val loginResponse: LoginResponse) : AuthResult()
    data class Error(val message: String) : AuthResult()
}

@Singleton
class AuthRepository @Inject constructor(
    private val apiService: ApiService,
    private val dataStore: DataStore<Preferences>,
    private val tokenProvider: TokenProvider
) {
    companion object {
        private val TOKEN_KEY = stringPreferencesKey(Constants.KEY_AUTH_TOKEN)
        private val USER_NAME_KEY = stringPreferencesKey(Constants.KEY_USER_NAME)
        private val FULL_NAME_KEY = stringPreferencesKey(Constants.KEY_FULL_NAME)
        private val HANDLOWIEC_NAME_KEY = stringPreferencesKey(Constants.KEY_HANDLOWIEC_NAME)
    }

    /**
     * Attempt login with username and password.
     */
    suspend fun login(username: String, password: String): AuthResult {
        return try {
            val response = apiService.login(LoginRequest(username, password))
            if (response.isSuccessful && response.body() != null) {
                val loginResponse = response.body()!!
                saveAuthData(loginResponse)
                tokenProvider.setToken(loginResponse.token)
                AuthResult.Success(loginResponse)
            } else {
                val errorMsg = when (response.code()) {
                    401 -> "Nieprawidlowa nazwa uzytkownika lub haslo"
                    403 -> "Brak dostepu do systemu"
                    500 -> "Blad serwera. Sprobuj ponownie pozniej"
                    else -> "Blad logowania: ${response.code()}"
                }
                AuthResult.Error(errorMsg)
            }
        } catch (e: Exception) {
            AuthResult.Error("Blad polaczenia z serwerem: ${e.localizedMessage ?: "Nieznany blad"}")
        }
    }

    /**
     * Save authentication data to DataStore.
     */
    private suspend fun saveAuthData(loginResponse: LoginResponse) {
        dataStore.edit { preferences ->
            preferences[TOKEN_KEY] = loginResponse.token
            preferences[USER_NAME_KEY] = loginResponse.userName
            preferences[FULL_NAME_KEY] = loginResponse.fullName
            preferences[HANDLOWIEC_NAME_KEY] = loginResponse.handlowiecName
        }
    }

    /**
     * Load saved token on app startup and set it in TokenProvider.
     */
    suspend fun loadSavedToken(): Boolean {
        val preferences = dataStore.data.first()
        val savedToken = preferences[TOKEN_KEY]
        return if (savedToken != null) {
            tokenProvider.setToken(savedToken)
            true
        } else {
            false
        }
    }

    /**
     * Get saved user name as Flow.
     */
    fun getUserName(): Flow<String> {
        return dataStore.data.map { preferences ->
            preferences[FULL_NAME_KEY] ?: ""
        }
    }

    /**
     * Get saved handlowiec name as Flow.
     */
    fun getHandlowiecName(): Flow<String> {
        return dataStore.data.map { preferences ->
            preferences[HANDLOWIEC_NAME_KEY] ?: ""
        }
    }

    /**
     * Clear all auth data (logout).
     */
    suspend fun logout() {
        tokenProvider.clearToken()
        dataStore.edit { preferences ->
            preferences.remove(TOKEN_KEY)
            preferences.remove(USER_NAME_KEY)
            preferences.remove(FULL_NAME_KEY)
            preferences.remove(HANDLOWIEC_NAME_KEY)
        }
    }

    /**
     * Check if user is currently logged in.
     */
    fun isLoggedIn(): Boolean {
        return tokenProvider.isLoggedIn()
    }
}
