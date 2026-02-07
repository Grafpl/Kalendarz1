package com.pronova.handlowiec.data.model

import com.google.gson.annotations.SerializedName

data class Order(
    @SerializedName("id")
    val id: Int = 0,

    @SerializedName("klientId")
    val klientId: Int = 0,

    @SerializedName("odbiorca")
    val odbiorca: String = "",

    @SerializedName("handlowiec")
    val handlowiec: String = "",

    @SerializedName("iloscZamowiona")
    val iloscZamowiona: Double = 0.0,

    @SerializedName("iloscFaktyczna")
    val iloscFaktyczna: Double = 0.0,

    @SerializedName("roznica")
    val roznica: Double = 0.0,

    @SerializedName("pojemniki")
    val pojemniki: Int = 0,

    @SerializedName("palety")
    val palety: Int = 0,

    @SerializedName("trybE2")
    val trybE2: Boolean = false,

    @SerializedName("dataPrzyjecia")
    val dataPrzyjecia: String = "",

    @SerializedName("godzinaPrzyjecia")
    val godzinaPrzyjecia: String = "",

    @SerializedName("dataUboju")
    val dataUboju: String = "",

    @SerializedName("status")
    val status: String = "",

    @SerializedName("maNotatke")
    val maNotatke: Boolean = false,

    @SerializedName("maFolie")
    val maFolie: Boolean = false,

    @SerializedName("maHallal")
    val maHallal: Boolean = false,

    @SerializedName("czyMaCeny")
    val czyMaCeny: Boolean = false,

    @SerializedName("sredniaCena")
    val sredniaCena: Double = 0.0,

    @SerializedName("uwagi")
    val uwagi: String = "",

    @SerializedName("transportKursId")
    val transportKursId: Int? = null,

    @SerializedName("czyZrealizowane")
    val czyZrealizowane: Boolean = false,

    @SerializedName("waluta")
    val waluta: String = "PLN",

    @SerializedName("pozycje")
    val pozycje: List<OrderItem> = emptyList()
)
