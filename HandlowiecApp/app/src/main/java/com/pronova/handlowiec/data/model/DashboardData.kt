package com.pronova.handlowiec.data.model

import com.google.gson.annotations.SerializedName

data class DashboardData(
    @SerializedName("data")
    val data: String = "",

    @SerializedName("sumaZamowien")
    val sumaZamowien: Double = 0.0,

    @SerializedName("liczbaZamowien")
    val liczbaZamowien: Int = 0,

    @SerializedName("liczbaKlientow")
    val liczbaKlientow: Int = 0,

    @SerializedName("sumaPalet")
    val sumaPalet: Int = 0,

    @SerializedName("liczbaAnulowanych")
    val liczbaAnulowanych: Int = 0
)
