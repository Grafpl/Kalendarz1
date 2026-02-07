package com.pronova.handlowiec.data.model

import com.google.gson.annotations.SerializedName

data class Customer(
    @SerializedName("id")
    val id: Int = 0,

    @SerializedName("shortcut")
    val shortcut: String = "",

    @SerializedName("nazwa")
    val nazwa: String = "",

    @SerializedName("handlowiec")
    val handlowiec: String = "",

    @SerializedName("nip")
    val nip: String = "",

    @SerializedName("adres")
    val adres: String = "",

    @SerializedName("miasto")
    val miasto: String = "",

    @SerializedName("kodPocztowy")
    val kodPocztowy: String = "",

    @SerializedName("telefon")
    val telefon: String = "",

    @SerializedName("email")
    val email: String = "",

    @SerializedName("terminPlatnosci")
    val terminPlatnosci: Int = 0,

    @SerializedName("limitKredytowy")
    val limitKredytowy: Double = 0.0,

    @SerializedName("saldoNaleznosci")
    val saldoNaleznosci: Double = 0.0,

    @SerializedName("aktywny")
    val aktywny: Boolean = true
)
