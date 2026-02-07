package com.pronova.handlowiec.data.model

import com.google.gson.annotations.SerializedName

data class Product(
    @SerializedName("id")
    val id: Int = 0,

    @SerializedName("kod")
    val kod: String = "",

    @SerializedName("nazwa")
    val nazwa: String = "",

    @SerializedName("katalog")
    val katalog: String = "",

    @SerializedName("jm")
    val jm: String = "kg",

    @SerializedName("cena")
    val cena: Double = 0.0,

    @SerializedName("aktywny")
    val aktywny: Boolean = true
)
