package com.pronova.handlowiec.data.model

import com.google.gson.annotations.SerializedName

data class OrderCreateRequest(
    @SerializedName("klientId")
    val klientId: Int,

    @SerializedName("dataUboju")
    val dataUboju: String,

    @SerializedName("dataPrzyjecia")
    val dataPrzyjecia: String,

    @SerializedName("uwagi")
    val uwagi: String = "",

    @SerializedName("waluta")
    val waluta: String = "PLN",

    @SerializedName("pozycje")
    val pozycje: List<OrderItemCreate> = emptyList()
)

data class OrderItemCreate(
    @SerializedName("kodTowaru")
    val kodTowaru: String,

    @SerializedName("ilosc")
    val ilosc: Double,

    @SerializedName("cena")
    val cena: Double,

    @SerializedName("folia")
    val folia: Boolean = false,

    @SerializedName("hallal")
    val hallal: Boolean = false
)
