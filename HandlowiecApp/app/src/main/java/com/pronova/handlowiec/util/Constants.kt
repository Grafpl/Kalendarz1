package com.pronova.handlowiec.util

object Constants {
    // Base URL for the REST API - change this to your server address
    const val BASE_URL = "https://api.pronova.pl/"

    // DataStore
    const val DATASTORE_NAME = "handlowiec_preferences"
    const val KEY_AUTH_TOKEN = "auth_token"
    const val KEY_USER_NAME = "user_name"
    const val KEY_FULL_NAME = "full_name"
    const val KEY_HANDLOWIEC_NAME = "handlowiec_name"

    // Date formats
    const val API_DATE_FORMAT = "yyyy-MM-dd"
    const val DISPLAY_DATE_FORMAT = "dd.MM.yyyy"
    const val DISPLAY_TIME_FORMAT = "HH:mm"
    const val DISPLAY_DATETIME_FORMAT = "dd.MM.yyyy HH:mm"

    // Currency
    const val CURRENCY_PLN = "PLN"
    const val CURRENCY_EUR = "EUR"

    // Order Status
    const val STATUS_NEW = "Nowe"
    const val STATUS_IN_PROGRESS = "W realizacji"
    const val STATUS_COMPLETED = "Zrealizowane"
    const val STATUS_CANCELLED = "Anulowane"
}
