package com.pronova.handlowiec.ui.orders

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.unit.dp
import androidx.compose.ui.window.Dialog
import androidx.hilt.navigation.compose.hiltViewModel
import com.pronova.handlowiec.data.model.Customer
import com.pronova.handlowiec.data.model.Product
import com.pronova.handlowiec.ui.components.LoadingIndicator
import com.pronova.handlowiec.ui.theme.PrimaryGreen
import com.pronova.handlowiec.ui.theme.StatusCancelled
import com.pronova.handlowiec.util.DateUtils
import java.time.LocalDate

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun OrderCreateScreen(
    onNavigateBack: () -> Unit,
    onOrderCreated: () -> Unit,
    viewModel: OrderCreateViewModel = hiltViewModel()
) {
    val uiState by viewModel.uiState.collectAsState()
    val selectedCustomer by viewModel.selectedCustomer.collectAsState()
    val slaughterDate by viewModel.slaughterDate.collectAsState()
    val acceptanceDate by viewModel.acceptanceDate.collectAsState()
    val notes by viewModel.notes.collectAsState()
    val currency by viewModel.currency.collectAsState()
    val items by viewModel.items.collectAsState()
    val customers by viewModel.customers.collectAsState()
    val products by viewModel.products.collectAsState()
    val isLoadingData by viewModel.isLoadingData.collectAsState()

    var showCustomerDialog by remember { mutableStateOf(false) }
    var showProductDialogForItemId by remember { mutableStateOf<Int?>(null) }
    var showSlaughterDatePicker by remember { mutableStateOf(false) }
    var showAcceptanceDatePicker by remember { mutableStateOf(false) }

    // Navigate on success
    LaunchedEffect(uiState) {
        if (uiState is OrderCreateUiState.Success) {
            onOrderCreated()
        }
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = {
                    Text(
                        "Nowe zamowienie",
                        fontWeight = FontWeight.SemiBold
                    )
                },
                navigationIcon = {
                    IconButton(onClick = onNavigateBack) {
                        Icon(
                            Icons.Default.ArrowBack,
                            contentDescription = "Wstecz",
                            tint = Color.White
                        )
                    }
                },
                colors = TopAppBarDefaults.topAppBarColors(
                    containerColor = PrimaryGreen,
                    titleContentColor = Color.White
                )
            )
        }
    ) { paddingValues ->
        if (isLoadingData) {
            LoadingIndicator(
                message = "Ladowanie danych...",
                modifier = Modifier.padding(paddingValues)
            )
        } else {
            Column(
                modifier = Modifier
                    .padding(paddingValues)
                    .fillMaxSize()
                    .verticalScroll(rememberScrollState())
                    .padding(16.dp),
                verticalArrangement = Arrangement.spacedBy(16.dp)
            ) {
                // Customer selector
                Text(
                    text = "Klient",
                    style = MaterialTheme.typography.titleMedium,
                    fontWeight = FontWeight.SemiBold
                )
                OutlinedCard(
                    modifier = Modifier
                        .fillMaxWidth()
                        .clickable { showCustomerDialog = true },
                    shape = RoundedCornerShape(12.dp)
                ) {
                    Row(
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(16.dp),
                        horizontalArrangement = Arrangement.SpaceBetween,
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Text(
                            text = selectedCustomer?.nazwa ?: "Wybierz klienta...",
                            style = MaterialTheme.typography.bodyLarge,
                            color = if (selectedCustomer != null)
                                MaterialTheme.colorScheme.onSurface
                            else MaterialTheme.colorScheme.onSurfaceVariant
                        )
                        Icon(
                            Icons.Default.ArrowDropDown,
                            contentDescription = null,
                            tint = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                    }
                }

                // Dates row
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.spacedBy(12.dp)
                ) {
                    // Slaughter date
                    Column(modifier = Modifier.weight(1f)) {
                        Text(
                            text = "Data uboju",
                            style = MaterialTheme.typography.titleSmall,
                            fontWeight = FontWeight.Medium
                        )
                        Spacer(modifier = Modifier.height(4.dp))
                        OutlinedCard(
                            modifier = Modifier.clickable { showSlaughterDatePicker = true },
                            shape = RoundedCornerShape(12.dp)
                        ) {
                            Row(
                                modifier = Modifier.padding(12.dp),
                                verticalAlignment = Alignment.CenterVertically
                            ) {
                                Icon(
                                    Icons.Default.CalendarToday,
                                    contentDescription = null,
                                    tint = PrimaryGreen,
                                    modifier = Modifier.size(18.dp)
                                )
                                Spacer(modifier = Modifier.width(8.dp))
                                Text(
                                    text = DateUtils.formatForDisplay(DateUtils.formatForApi(slaughterDate)),
                                    style = MaterialTheme.typography.bodyMedium
                                )
                            }
                        }
                    }

                    // Acceptance date
                    Column(modifier = Modifier.weight(1f)) {
                        Text(
                            text = "Data przyjecia",
                            style = MaterialTheme.typography.titleSmall,
                            fontWeight = FontWeight.Medium
                        )
                        Spacer(modifier = Modifier.height(4.dp))
                        OutlinedCard(
                            modifier = Modifier.clickable { showAcceptanceDatePicker = true },
                            shape = RoundedCornerShape(12.dp)
                        ) {
                            Row(
                                modifier = Modifier.padding(12.dp),
                                verticalAlignment = Alignment.CenterVertically
                            ) {
                                Icon(
                                    Icons.Default.CalendarToday,
                                    contentDescription = null,
                                    tint = PrimaryGreen,
                                    modifier = Modifier.size(18.dp)
                                )
                                Spacer(modifier = Modifier.width(8.dp))
                                Text(
                                    text = DateUtils.formatForDisplay(DateUtils.formatForApi(acceptanceDate)),
                                    style = MaterialTheme.typography.bodyMedium
                                )
                            }
                        }
                    }
                }

                // Currency selector
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.spacedBy(8.dp),
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Text(
                        text = "Waluta:",
                        style = MaterialTheme.typography.titleSmall,
                        fontWeight = FontWeight.Medium
                    )
                    FilterChip(
                        selected = currency == "PLN",
                        onClick = { viewModel.onCurrencyChange("PLN") },
                        label = { Text("PLN") },
                        colors = FilterChipDefaults.filterChipColors(
                            selectedContainerColor = PrimaryGreen,
                            selectedLabelColor = Color.White
                        )
                    )
                    FilterChip(
                        selected = currency == "EUR",
                        onClick = { viewModel.onCurrencyChange("EUR") },
                        label = { Text("EUR") },
                        colors = FilterChipDefaults.filterChipColors(
                            selectedContainerColor = PrimaryGreen,
                            selectedLabelColor = Color.White
                        )
                    )
                }

                // Notes
                OutlinedTextField(
                    value = notes,
                    onValueChange = viewModel::onNotesChange,
                    modifier = Modifier.fillMaxWidth(),
                    label = { Text("Uwagi") },
                    shape = RoundedCornerShape(12.dp),
                    minLines = 2,
                    maxLines = 4,
                    colors = OutlinedTextFieldDefaults.colors(
                        focusedBorderColor = PrimaryGreen,
                        focusedLabelColor = PrimaryGreen,
                        cursorColor = PrimaryGreen
                    )
                )

                // Items section
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.SpaceBetween,
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Text(
                        text = "Pozycje (${items.size})",
                        style = MaterialTheme.typography.titleMedium,
                        fontWeight = FontWeight.SemiBold
                    )
                    TextButton(onClick = { viewModel.addItem() }) {
                        Icon(Icons.Default.Add, contentDescription = null, modifier = Modifier.size(18.dp))
                        Spacer(modifier = Modifier.width(4.dp))
                        Text("Dodaj pozycje")
                    }
                }

                items.forEach { item ->
                    OrderFormItemCard(
                        item = item,
                        onProductClick = { showProductDialogForItemId = item.id },
                        onQuantityChange = { value ->
                            viewModel.updateItem(item.id) { it.copy(quantity = value) }
                        },
                        onPriceChange = { value ->
                            viewModel.updateItem(item.id) { it.copy(price = value) }
                        },
                        onFoliaChange = { value ->
                            viewModel.updateItem(item.id) { it.copy(folia = value) }
                        },
                        onHallalChange = { value ->
                            viewModel.updateItem(item.id) { it.copy(hallal = value) }
                        },
                        onRemove = { viewModel.removeItem(item.id) },
                        canRemove = items.size > 1
                    )
                }

                // Error/Validation display
                when (val state = uiState) {
                    is OrderCreateUiState.ValidationError -> {
                        Text(
                            text = state.message,
                            color = StatusCancelled,
                            style = MaterialTheme.typography.bodyMedium,
                            modifier = Modifier.fillMaxWidth()
                        )
                    }
                    is OrderCreateUiState.Error -> {
                        Text(
                            text = state.message,
                            color = StatusCancelled,
                            style = MaterialTheme.typography.bodyMedium,
                            modifier = Modifier.fillMaxWidth()
                        )
                    }
                    else -> {}
                }

                // Save button
                Button(
                    onClick = { viewModel.saveOrder() },
                    modifier = Modifier
                        .fillMaxWidth()
                        .height(52.dp),
                    shape = RoundedCornerShape(12.dp),
                    colors = ButtonDefaults.buttonColors(containerColor = PrimaryGreen),
                    enabled = uiState !is OrderCreateUiState.Saving
                ) {
                    if (uiState is OrderCreateUiState.Saving) {
                        CircularProgressIndicator(
                            color = Color.White,
                            modifier = Modifier.size(24.dp),
                            strokeWidth = 2.dp
                        )
                        Spacer(modifier = Modifier.width(12.dp))
                        Text("Zapisywanie...", fontWeight = FontWeight.SemiBold)
                    } else {
                        Icon(Icons.Default.Save, contentDescription = null, modifier = Modifier.size(20.dp))
                        Spacer(modifier = Modifier.width(8.dp))
                        Text("Zapisz zamowienie", fontWeight = FontWeight.SemiBold)
                    }
                }

                Spacer(modifier = Modifier.height(16.dp))
            }
        }
    }

    // Customer selection dialog
    if (showCustomerDialog) {
        SearchableListDialog(
            title = "Wybierz klienta",
            items = customers,
            searchPlaceholder = "Szukaj klienta...",
            itemText = { "${it.shortcut} - ${it.nazwa}" },
            itemSubtext = { "${it.miasto}" },
            onItemSelected = { customer ->
                viewModel.onCustomerSelected(customer)
                showCustomerDialog = false
            },
            onDismiss = { showCustomerDialog = false },
            filterPredicate = { customer, query ->
                customer.nazwa.contains(query, ignoreCase = true) ||
                customer.shortcut.contains(query, ignoreCase = true) ||
                customer.miasto.contains(query, ignoreCase = true)
            }
        )
    }

    // Product selection dialog
    showProductDialogForItemId?.let { itemId ->
        SearchableListDialog(
            title = "Wybierz produkt",
            items = products,
            searchPlaceholder = "Szukaj produktu...",
            itemText = { "${it.kod} - ${it.nazwa}" },
            itemSubtext = { "${it.katalog} | ${it.jm}" },
            onItemSelected = { product ->
                viewModel.updateItem(itemId) { it.copy(product = product, price = product.cena.toString()) }
                showProductDialogForItemId = null
            },
            onDismiss = { showProductDialogForItemId = null },
            filterPredicate = { product, query ->
                product.nazwa.contains(query, ignoreCase = true) ||
                product.kod.contains(query, ignoreCase = true) ||
                product.katalog.contains(query, ignoreCase = true)
            }
        )
    }

    // Date pickers
    if (showSlaughterDatePicker) {
        DatePickerDialogWrapper(
            initialDate = slaughterDate,
            onDateSelected = {
                viewModel.onSlaughterDateSelected(it)
                showSlaughterDatePicker = false
            },
            onDismiss = { showSlaughterDatePicker = false }
        )
    }

    if (showAcceptanceDatePicker) {
        DatePickerDialogWrapper(
            initialDate = acceptanceDate,
            onDateSelected = {
                viewModel.onAcceptanceDateSelected(it)
                showAcceptanceDatePicker = false
            },
            onDismiss = { showAcceptanceDatePicker = false }
        )
    }
}

@Composable
fun OrderFormItemCard(
    item: OrderFormItem,
    onProductClick: () -> Unit,
    onQuantityChange: (String) -> Unit,
    onPriceChange: (String) -> Unit,
    onFoliaChange: (Boolean) -> Unit,
    onHallalChange: (Boolean) -> Unit,
    onRemove: () -> Unit,
    canRemove: Boolean
) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(12.dp),
        colors = CardDefaults.cardColors(containerColor = Color.White),
        elevation = CardDefaults.cardElevation(defaultElevation = 1.dp)
    ) {
        Column(modifier = Modifier.padding(12.dp)) {
            // Product selector + remove
            Row(
                modifier = Modifier.fillMaxWidth(),
                verticalAlignment = Alignment.CenterVertically
            ) {
                OutlinedCard(
                    modifier = Modifier
                        .weight(1f)
                        .clickable(onClick = onProductClick),
                    shape = RoundedCornerShape(8.dp)
                ) {
                    Text(
                        text = item.product?.let { "${it.kod} - ${it.nazwa}" } ?: "Wybierz produkt...",
                        modifier = Modifier.padding(12.dp),
                        style = MaterialTheme.typography.bodyMedium,
                        color = if (item.product != null)
                            MaterialTheme.colorScheme.onSurface
                        else MaterialTheme.colorScheme.onSurfaceVariant
                    )
                }
                if (canRemove) {
                    IconButton(onClick = onRemove) {
                        Icon(
                            Icons.Default.Delete,
                            contentDescription = "Usun pozycje",
                            tint = StatusCancelled
                        )
                    }
                }
            }

            Spacer(modifier = Modifier.height(8.dp))

            // Quantity and price
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(8.dp)
            ) {
                OutlinedTextField(
                    value = item.quantity,
                    onValueChange = onQuantityChange,
                    modifier = Modifier.weight(1f),
                    label = { Text("Ilosc (kg)") },
                    shape = RoundedCornerShape(8.dp),
                    singleLine = true,
                    keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Decimal),
                    colors = OutlinedTextFieldDefaults.colors(
                        focusedBorderColor = PrimaryGreen,
                        focusedLabelColor = PrimaryGreen,
                        cursorColor = PrimaryGreen
                    )
                )
                OutlinedTextField(
                    value = item.price,
                    onValueChange = onPriceChange,
                    modifier = Modifier.weight(1f),
                    label = { Text("Cena") },
                    shape = RoundedCornerShape(8.dp),
                    singleLine = true,
                    keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Decimal),
                    colors = OutlinedTextFieldDefaults.colors(
                        focusedBorderColor = PrimaryGreen,
                        focusedLabelColor = PrimaryGreen,
                        cursorColor = PrimaryGreen
                    )
                )
            }

            Spacer(modifier = Modifier.height(4.dp))

            // Checkboxes
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(16.dp)
            ) {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Checkbox(
                        checked = item.folia,
                        onCheckedChange = onFoliaChange,
                        colors = CheckboxDefaults.colors(checkedColor = PrimaryGreen)
                    )
                    Text("Folia", style = MaterialTheme.typography.bodyMedium)
                }
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Checkbox(
                        checked = item.hallal,
                        onCheckedChange = onHallalChange,
                        colors = CheckboxDefaults.colors(checkedColor = PrimaryGreen)
                    )
                    Text("Hallal", style = MaterialTheme.typography.bodyMedium)
                }
            }
        }
    }
}

@Composable
fun <T> SearchableListDialog(
    title: String,
    items: List<T>,
    searchPlaceholder: String,
    itemText: (T) -> String,
    itemSubtext: (T) -> String,
    onItemSelected: (T) -> Unit,
    onDismiss: () -> Unit,
    filterPredicate: (T, String) -> Boolean
) {
    var searchQuery by remember { mutableStateOf("") }

    val filteredItems = if (searchQuery.isBlank()) items
    else items.filter { filterPredicate(it, searchQuery) }

    Dialog(onDismissRequest = onDismiss) {
        Card(
            modifier = Modifier
                .fillMaxWidth()
                .heightIn(max = 500.dp),
            shape = RoundedCornerShape(16.dp)
        ) {
            Column {
                // Header
                Text(
                    text = title,
                    style = MaterialTheme.typography.titleLarge,
                    fontWeight = FontWeight.SemiBold,
                    modifier = Modifier.padding(16.dp)
                )

                // Search
                OutlinedTextField(
                    value = searchQuery,
                    onValueChange = { searchQuery = it },
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(horizontal = 16.dp),
                    placeholder = { Text(searchPlaceholder) },
                    leadingIcon = {
                        Icon(Icons.Default.Search, contentDescription = null)
                    },
                    shape = RoundedCornerShape(8.dp),
                    singleLine = true,
                    colors = OutlinedTextFieldDefaults.colors(
                        focusedBorderColor = PrimaryGreen,
                        cursorColor = PrimaryGreen
                    )
                )

                Spacer(modifier = Modifier.height(8.dp))

                // Items list
                LazyColumn(
                    modifier = Modifier.weight(1f)
                ) {
                    items(filteredItems) { item ->
                        Column(
                            modifier = Modifier
                                .fillMaxWidth()
                                .clickable { onItemSelected(item) }
                                .padding(horizontal = 16.dp, vertical = 12.dp)
                        ) {
                            Text(
                                text = itemText(item),
                                style = MaterialTheme.typography.bodyLarge,
                                fontWeight = FontWeight.Medium
                            )
                            Text(
                                text = itemSubtext(item),
                                style = MaterialTheme.typography.bodySmall,
                                color = MaterialTheme.colorScheme.onSurfaceVariant
                            )
                        }
                        HorizontalDivider(modifier = Modifier.padding(horizontal = 16.dp))
                    }
                }

                // Cancel button
                TextButton(
                    onClick = onDismiss,
                    modifier = Modifier
                        .align(Alignment.End)
                        .padding(8.dp)
                ) {
                    Text("Anuluj")
                }
            }
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun DatePickerDialogWrapper(
    initialDate: LocalDate,
    onDateSelected: (LocalDate) -> Unit,
    onDismiss: () -> Unit
) {
    val datePickerState = rememberDatePickerState(
        initialSelectedDateMillis = initialDate.toEpochDay() * 86400000L
    )

    DatePickerDialog(
        onDismissRequest = onDismiss,
        confirmButton = {
            TextButton(
                onClick = {
                    datePickerState.selectedDateMillis?.let { millis ->
                        val apiDate = DateUtils.millisToApiDate(millis)
                        DateUtils.parseApiDate(apiDate)?.let { date ->
                            onDateSelected(date)
                        }
                    }
                }
            ) {
                Text("OK")
            }
        },
        dismissButton = {
            TextButton(onClick = onDismiss) {
                Text("Anuluj")
            }
        }
    ) {
        DatePicker(state = datePickerState)
    }
}
