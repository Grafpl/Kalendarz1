package com.pronova.handlowiec.data.model

import com.google.gson.annotations.SerializedName

data class OrderItem(
    @SerializedName("zamowienieId")
    val zamowienieId: Int = 0,

    @SerializedName("kodTowaru")
    val kodTowaru: String = "",

    @SerializedName("nazwaTowaru")
    val nazwaTowaru: String = "",

    @SerializedName("ilosc")
    val ilosc: Double = 0.0,

    @SerializedName("cena")
    val cena: Double = 0.0,

    @SerializedName("pojemniki")
    val pojemniki: Int = 0,

    @SerializedName("palety")
    val palety: Int = 0,

    @SerializedName("e2")
    val e2: Boolean = false,

    @SerializedName("folia")
    val folia: Boolean = false,

    @SerializedName("hallal")
    val hallal: Boolean = false,

    @SerializedName("wydano")
    val wydano: Double = 0.0
)
