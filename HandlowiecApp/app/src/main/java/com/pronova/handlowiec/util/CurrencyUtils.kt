package com.pronova.handlowiec.util

import java.text.DecimalFormat
import java.text.DecimalFormatSymbols
import java.util.Locale

object CurrencyUtils {

    private val polishLocale = Locale("pl", "PL")

    private val priceFormat = DecimalFormat("#,##0.00", DecimalFormatSymbols(polishLocale))
    private val quantityFormat = DecimalFormat("#,##0.0", DecimalFormatSymbols(polishLocale))
    private val integerFormat = DecimalFormat("#,##0", DecimalFormatSymbols(polishLocale))

    /**
     * Format amount as PLN currency
     */
    fun formatPLN(amount: Double): String {
        return "${priceFormat.format(amount)} zl"
    }

    /**
     * Format amount as EUR currency
     */
    fun formatEUR(amount: Double): String {
        return "${priceFormat.format(amount)} EUR"
    }

    /**
     * Format amount with currency code
     */
    fun formatCurrency(amount: Double, currency: String): String {
        return when (currency.uppercase()) {
            Constants.CURRENCY_EUR -> formatEUR(amount)
            else -> formatPLN(amount)
        }
    }

    /**
     * Format price (2 decimal places)
     */
    fun formatPrice(price: Double): String {
        return priceFormat.format(price)
    }

    /**
     * Format quantity in kg (1 decimal place)
     */
    fun formatKg(quantity: Double): String {
        return "${quantityFormat.format(quantity)} kg"
    }

    /**
     * Format quantity without unit
     */
    fun formatQuantity(quantity: Double): String {
        return quantityFormat.format(quantity)
    }

    /**
     * Format integer value
     */
    fun formatInteger(value: Int): String {
        return integerFormat.format(value)
    }

    /**
     * Format percentage
     */
    fun formatPercent(value: Double): String {
        return "${priceFormat.format(value)}%"
    }

    /**
     * Calculate credit usage percentage
     */
    fun calculateCreditUsage(balance: Double, limit: Double): Double {
        if (limit <= 0.0) return 0.0
        return (balance / limit * 100.0).coerceIn(0.0, 100.0)
    }
}
