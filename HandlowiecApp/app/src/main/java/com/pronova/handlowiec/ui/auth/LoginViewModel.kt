package com.pronova.handlowiec.ui.auth

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.pronova.handlowiec.data.repository.AuthRepository
import com.pronova.handlowiec.data.repository.AuthResult
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch
import javax.inject.Inject

sealed class LoginUiState {
    data object Idle : LoginUiState()
    data object Loading : LoginUiState()
    data object Success : LoginUiState()
    data class Error(val message: String) : LoginUiState()
}

@HiltViewModel
class LoginViewModel @Inject constructor(
    private val authRepository: AuthRepository
) : ViewModel() {

    private val _uiState = MutableStateFlow<LoginUiState>(LoginUiState.Idle)
    val uiState: StateFlow<LoginUiState> = _uiState.asStateFlow()

    private val _username = MutableStateFlow("")
    val username: StateFlow<String> = _username.asStateFlow()

    private val _password = MutableStateFlow("")
    val password: StateFlow<String> = _password.asStateFlow()

    fun onUsernameChange(value: String) {
        _username.value = value
        if (_uiState.value is LoginUiState.Error) {
            _uiState.value = LoginUiState.Idle
        }
    }

    fun onPasswordChange(value: String) {
        _password.value = value
        if (_uiState.value is LoginUiState.Error) {
            _uiState.value = LoginUiState.Idle
        }
    }

    fun login() {
        val user = _username.value.trim()
        val pass = _password.value.trim()

        if (user.isEmpty() || pass.isEmpty()) {
            _uiState.value = LoginUiState.Error("Wypelnij wszystkie pola")
            return
        }

        viewModelScope.launch {
            _uiState.value = LoginUiState.Loading
            when (val result = authRepository.login(user, pass)) {
                is AuthResult.Success -> {
                    _uiState.value = LoginUiState.Success
                }
                is AuthResult.Error -> {
                    _uiState.value = LoginUiState.Error(result.message)
                }
            }
        }
    }

    /**
     * Check if user has a saved session. If so, skip login.
     */
    fun checkSavedSession() {
        viewModelScope.launch {
            val hasToken = authRepository.loadSavedToken()
            if (hasToken) {
                _uiState.value = LoginUiState.Success
            }
        }
    }
}
