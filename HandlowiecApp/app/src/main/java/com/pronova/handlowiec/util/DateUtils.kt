package com.pronova.handlowiec.util

import java.text.SimpleDateFormat
import java.time.LocalDate
import java.time.LocalDateTime
import java.time.format.DateTimeFormatter
import java.util.Date
import java.util.Locale

object DateUtils {

    private val polishLocale = Locale("pl", "PL")

    private val apiDateFormatter = DateTimeFormatter.ofPattern(Constants.API_DATE_FORMAT, polishLocale)
    private val displayDateFormatter = DateTimeFormatter.ofPattern(Constants.DISPLAY_DATE_FORMAT, polishLocale)
    private val displayTimeFormatter = DateTimeFormatter.ofPattern(Constants.DISPLAY_TIME_FORMAT, polishLocale)
    private val displayDateTimeFormatter = DateTimeFormatter.ofPattern(Constants.DISPLAY_DATETIME_FORMAT, polishLocale)

    /**
     * Format a LocalDate for API calls (yyyy-MM-dd)
     */
    fun formatForApi(date: LocalDate): String {
        return date.format(apiDateFormatter)
    }

    /**
     * Format a date string from API format to display format (dd.MM.yyyy)
     */
    fun formatForDisplay(apiDate: String): String {
        return try {
            val date = LocalDate.parse(apiDate, apiDateFormatter)
            date.format(displayDateFormatter)
        } catch (e: Exception) {
            apiDate
        }
    }

    /**
     * Format time for display (HH:mm)
     */
    fun formatTimeForDisplay(time: String): String {
        return try {
            if (time.length >= 5) time.substring(0, 5) else time
        } catch (e: Exception) {
            time
        }
    }

    /**
     * Get today's date formatted for API
     */
    fun todayForApi(): String {
        return LocalDate.now().format(apiDateFormatter)
    }

    /**
     * Get today's date formatted for display
     */
    fun todayForDisplay(): String {
        return LocalDate.now().format(displayDateFormatter)
    }

    /**
     * Parse API date string to LocalDate
     */
    fun parseApiDate(apiDate: String): LocalDate? {
        return try {
            LocalDate.parse(apiDate, apiDateFormatter)
        } catch (e: Exception) {
            null
        }
    }

    /**
     * Convert millis to API date format
     */
    fun millisToApiDate(millis: Long): String {
        val sdf = SimpleDateFormat(Constants.API_DATE_FORMAT, polishLocale)
        return sdf.format(Date(millis))
    }

    /**
     * Convert millis to display date format
     */
    fun millisToDisplayDate(millis: Long): String {
        val sdf = SimpleDateFormat(Constants.DISPLAY_DATE_FORMAT, polishLocale)
        return sdf.format(Date(millis))
    }

    /**
     * Get Polish day of week name
     */
    fun getDayOfWeekName(date: LocalDate): String {
        val dayNames = mapOf(
            java.time.DayOfWeek.MONDAY to "Poniedzialek",
            java.time.DayOfWeek.TUESDAY to "Wtorek",
            java.time.DayOfWeek.WEDNESDAY to "Sroda",
            java.time.DayOfWeek.THURSDAY to "Czwartek",
            java.time.DayOfWeek.FRIDAY to "Piatek",
            java.time.DayOfWeek.SATURDAY to "Sobota",
            java.time.DayOfWeek.SUNDAY to "Niedziela"
        )
        return dayNames[date.dayOfWeek] ?: ""
    }
}
