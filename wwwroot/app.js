// The browser keeps only the short-lived access token and uses the refresh token when needed.
const session = {
    accessToken: localStorage.getItem("ledgerly.accessToken"),
    refreshToken: localStorage.getItem("ledgerly.refreshToken"),
    user: JSON.parse(localStorage.getItem("ledgerly.user") || "null"),
}
let authMode = "login"
let selectedGroup = null
let groups = []
let selectedExpenseParticipants = []
let expenseMode = "group"
let viewingDirectExpenses = false

// All API calls pass through this helper so authentication and error messages stay consistent.
async function api(path, options = {}, retry = true) {
    const headers = { "Content-Type": "application/json", ...(options.headers || {}) }
    if (session.accessToken) headers.Authorization = `Bearer ${session.accessToken}`
    const response = await fetch(path, { ...options, headers })
    if (response.status === 401 && retry && session.refreshToken) {
        const refreshed = await refreshSession()
        if (refreshed) return api(path, options, false)
    }
    const body = await response.json().catch(() => null)
    if (!response.ok)
        throw new Error(formatApiError(body) || `Request failed (${response.status}).`)
    return body
}

// ASP.NET validation responses contain a dictionary, so flatten it into a useful message.
function formatApiError(body) {
    if (!body) return ""
    if (typeof body === "string") return body
    if (body.errors) return Object.values(body.errors).flat().join(" ")
    return body.title || body.message || ""
}

// Refresh tokens are rotated by the backend, which means the returned pair replaces both stored values.
async function refreshSession() {
    try {
        const response = await fetch("/api/auth/refresh", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ refreshToken: session.refreshToken }),
        })
        if (!response.ok) return false
        saveSession(await response.json(), session.user)
        return true
    } catch {
        return false
    }
}

// Persisting one small session object makes a page reload feel natural without storing passwords.
function saveSession(auth, user) {
    session.accessToken = auth.accessToken
    session.refreshToken = auth.refreshToken
    session.user = user
    localStorage.setItem("ledgerly.accessToken", auth.accessToken)
    localStorage.setItem("ledgerly.refreshToken", auth.refreshToken)
    localStorage.setItem("ledgerly.user", JSON.stringify(user))
}

function clearSession() {
    session.accessToken = null
    session.refreshToken = null
    session.user = null
    localStorage.removeItem("ledgerly.accessToken")
    localStorage.removeItem("ledgerly.refreshToken")
    localStorage.removeItem("ledgerly.user")
}

const elements = {
    authView: document.querySelector("#auth-view"),
    dashboard: document.querySelector("#dashboard-view"),
    authForm: document.querySelector("#auth-form"),
    authMessage: document.querySelector("#auth-message"),
    emailField: document.querySelector("#email-field"),
    authSubmitLabel: document.querySelector("#auth-submit-label"),
    userName: document.querySelector("#user-name"),
    headingName: document.querySelector("#heading-name"),
    logout: document.querySelector("#logout-button"),
    notificationsButton: document.querySelector("#notifications-button"),
    notificationCount: document.querySelector("#notification-count"),
    notificationsPanel: document.querySelector("#notifications-panel"),
    notificationsList: document.querySelector("#notifications-list"),
    groupsList: document.querySelector("#groups-list"),
    expensesList: document.querySelector("#expenses-list"),
    groupCount: document.querySelector("#group-count"),
    expenseCount: document.querySelector("#expense-count"),
    selectedGroupLabel: document.querySelector("#selected-group-label"),
    expenseFilter: document.querySelector("#expense-filter"),
    directExpensesButton: document.querySelector("#direct-expenses-button"),
    newExpense: document.querySelector("#new-expense-button"),
    expenseModal: document.querySelector("#expense-modal"),
    expenseForm: document.querySelector("#expense-form"),
    expenseGroupFields: document.querySelector("#expense-group-fields"),
    expenseGroup: document.querySelector("#expense-group"),
    expenseUserField: document.querySelector("#expense-user-field"),
    expenseUserHint: document.querySelector("#expense-user-hint"),
    expenseDescription: document.querySelector("#expense-description"),
    expenseAmount: document.querySelector("#expense-amount"),
    expenseCurrency: document.querySelector("#expense-currency"),
    expenseUserSearch: document.querySelector("#expense-user-search"),
    expenseUserResults: document.querySelector("#expense-user-results"),
    expenseSelectedUsers: document.querySelector("#expense-selected-users"),
    expenseMessage: document.querySelector("#expense-message"),
    budgetForm: document.querySelector("#budget-form"),
    budgetMessage: document.querySelector("#budget-message"),
    converterForm: document.querySelector("#converter-form"),
    conversionResult: document.querySelector("#conversion-result"),
    toast: document.querySelector("#toast"),
    groupModal: document.querySelector("#group-modal"),
    groupForm: document.querySelector("#group-form"),
    groupName: document.querySelector("#group-name"),
    groupUserSearch: document.querySelector("#group-user-search"),
    groupUserResults: document.querySelector("#group-user-results"),
    groupSelectedUsers: document.querySelector("#group-selected-users"),
    groupMessage: document.querySelector("#group-message"),
}
let selectedGroupUsers = []

// Switching auth mode keeps registration fields out of the sign-in request entirely.
document.querySelectorAll("[data-auth-mode]").forEach((button) =>
    button.addEventListener("click", () => {
        authMode = button.dataset.authMode
        document
            .querySelectorAll("[data-auth-mode]")
            .forEach((tab) => tab.classList.toggle("is-active", tab === button))
        elements.authSubmitLabel.textContent = authMode === "login" ? "Sign in" : "Create account"
        elements.emailField.classList.toggle("hidden", authMode === "login")
        document.querySelector("#email").required = authMode === "register"
        elements.authMessage.textContent = ""
    }),
)

elements.authForm.addEventListener("submit", async (event) => {
    event.preventDefault()
    const form = new FormData(event.currentTarget)
    const payload = { username: form.get("username"), password: form.get("password") }
    if (authMode === "register") payload.email = form.get("email")
    try {
        elements.authMessage.textContent = "Connecting..."
        const auth = await api(
            `/api/auth/${authMode}`,
            { method: "POST", body: JSON.stringify(payload) },
            false,
        )
        const user = {
            username: payload.username,
            displayName: payload.username,
            email: payload.email || payload.username,
        }
        saveSession(auth, user)
        showDashboard()
    } catch (error) {
        elements.authMessage.textContent = error.message
    }
})

elements.logout.addEventListener("click", () => {
    clearSession()
    showAuth()
})
document.querySelector("#refresh-button").addEventListener("click", loadDashboard)
document.querySelector("#new-group-button").addEventListener("click", openGroupModal)
document.querySelector("#close-group-modal").addEventListener("click", closeGroupModal)
elements.groupModal.addEventListener("click", (event) => {
    if (event.target === elements.groupModal) closeGroupModal()
})
elements.groupForm.addEventListener("submit", createGroup)
elements.groupUserSearch.addEventListener("input", searchGroupUsers)
elements.newExpense.addEventListener("click", openExpenseModal)
elements.directExpensesButton.addEventListener("click", loadDirectExpenses)
elements.expenseFilter.addEventListener("change", () => {
    if (elements.expenseFilter.value === "direct") loadDirectExpenses()
    else if (elements.expenseFilter.value) selectGroup(elements.expenseFilter.value)
})
document
    .querySelectorAll("[data-expense-mode]")
    .forEach((button) =>
        button.addEventListener("click", () => setExpenseMode(button.dataset.expenseMode)),
    )
elements.expenseGroup.addEventListener("change", () => {
    selectedGroup = groups.find((group) => group.id === elements.expenseGroup.value)
    selectedExpenseParticipants = []
    addCurrentUserToExpense()
    renderSelectedExpenseUsers()
})
document.querySelector("#close-expense-modal").addEventListener("click", closeExpenseModal)
elements.expenseModal.addEventListener("click", (event) => {
    if (event.target === elements.expenseModal) closeExpenseModal()
})
elements.expenseForm.addEventListener("submit", createExpense)
elements.expenseAmount.addEventListener("input", resetExpenseSplitAmounts)
elements.expenseUserSearch.addEventListener("input", searchExpenseUsers)
elements.notificationsButton.addEventListener("click", () => {
    elements.notificationsPanel.classList.toggle("hidden")
    if (!elements.notificationsPanel.classList.contains("hidden")) loadNotifications()
})
elements.budgetForm.addEventListener("submit", createBudget)
elements.converterForm.addEventListener("submit", convertCurrency)

// The dashboard starts by loading groups; the selected group then drives the expense list.
async function loadDashboard() {
    try {
        groups = await api("/api/groups")
        elements.groupCount.textContent = groups.length
        populateExpenseFilter()
        await loadNotifications()
        renderGroups()
        if (selectedGroup && groups.some((group) => group.id === selectedGroup.id))
            await selectGroup(selectedGroup.id)
        else if (groups[0]) await selectGroup(groups[0].id)
        else renderExpenses([])
    } catch (error) {
        showToast(error.message)
    }
}

function renderGroups() {
    if (!groups.length) {
        elements.groupsList.innerHTML =
            '<div class="empty-state">No groups yet. Create one to start sharing.</div>'
        return
    }
    elements.groupsList.innerHTML = groups
        .map(
            (group) =>
                `<button class="group-row ${selectedGroup?.id === group.id ? "is-selected" : ""}" data-group-id="${group.id}" type="button"><span><span class="group-name">${escapeHtml(group.name)}</span><span class="group-meta">${group.members.length} member${group.members.length === 1 ? "" : "s"}</span></span><span class="group-meta">→</span></button>`,
        )
        .join("")
    elements.groupsList
        .querySelectorAll("[data-group-id]")
        .forEach((button) =>
            button.addEventListener("click", () => selectGroup(button.dataset.groupId)),
        )
}

async function selectGroup(groupId) {
    selectedGroup = groups.find((group) => group.id === groupId)
    if (!selectedGroup) {
        showToast("Select a valid group before adding an expense.")
        return
    }
    renderGroups()
    viewingDirectExpenses = false
    elements.expenseFilter.value = selectedGroup.id
    elements.selectedGroupLabel.textContent = selectedGroup.name
    elements.newExpense.disabled = false
    try {
        renderExpenses(await api(`/api/groups/${groupId}/expenses`))
    } catch (error) {
        showToast(error.message)
    }
}

async function loadDirectExpenses() {
    viewingDirectExpenses = true
    selectedGroup = null
    elements.expenseFilter.value = "direct"
    elements.selectedGroupLabel.textContent = "Direct expenses"
    try {
        renderExpenses(await api("/api/expenses"))
    } catch (error) {
        showToast(error.message)
    }
}

function populateExpenseFilter() {
    const currentValue = elements.expenseFilter.value
    elements.expenseFilter.innerHTML =
        '<option value="">Filter expenses</option><option value="direct">Direct expenses</option>' +
        groups
            .map((group) => `<option value="${group.id}">${escapeHtml(group.name)}</option>`)
            .join("")
    if (
        currentValue &&
        [...elements.expenseFilter.options].some((option) => option.value === currentValue)
    )
        elements.expenseFilter.value = currentValue
}

function renderExpenses(expenses) {
    elements.expenseCount.textContent = expenses.length
    if (!expenses.length) {
        elements.expensesList.innerHTML =
            '<div class="empty-state">No expenses here yet. Add the first one.</div>'
        return
    }
    elements.expensesList.innerHTML = expenses
        .slice()
        .reverse()
        .map((expense) => {
            const yourSplit = expense.splits.find(
                (split) => split.username?.toLowerCase() === session.user.username?.toLowerCase(),
            )
            const canSettle =
                yourSplit &&
                expense.payerUsername?.toLowerCase() !== session.user.username?.toLowerCase()
            const settlement = !yourSplit
                ? "Not part of your split"
                : canSettle && yourSplit.settledAtUtc
                  ? "Settled"
                  : canSettle
                    ? `You owe ${Number(yourSplit.amount).toFixed(2)} ${escapeHtml(expense.currency)}`
                    : "You paid"
            const settleAction =
                canSettle && !yourSplit.settledAtUtc
                    ? `<button class="button button-light button-small" data-settle-expense="${expense.id}" type="button">Settle</button>`
                    : ""
            return `<div class="expense-row"><span><span class="expense-title">${escapeHtml(expense.description)}</span><span class="expense-meta">${new Date(expense.spentAtUtc).toLocaleDateString()} · paid by @${escapeHtml(expense.payerUsername || "user")} · ${settlement}</span></span><span class="expense-actions"><span class="expense-amount">${Number(expense.amount).toFixed(2)} ${escapeHtml(expense.currency)}</span>${settleAction}</span></div>`
        })
        .join("")
    elements.expensesList
        .querySelectorAll("[data-settle-expense]")
        .forEach((button) =>
            button.addEventListener("click", () => settleExpense(button.dataset.settleExpense)),
        )
}

async function settleExpense(expenseId) {
    try {
        const path = viewingDirectExpenses
            ? `/api/expenses/${expenseId}/settle`
            : `/api/groups/${selectedGroup.id}/expenses/${expenseId}/settle`
        await api(path, {
            method: "POST",
        })
        showToast("Expense settled.")
        if (viewingDirectExpenses) await loadDirectExpenses()
        else await selectGroup(selectedGroup.id)
    } catch (error) {
        showToast(error.message)
    }
}

function openGroupModal() {
    selectedGroupUsers = []
    elements.groupForm.reset()
    elements.groupUserResults.innerHTML = ""
    elements.groupSelectedUsers.innerHTML = ""
    elements.groupMessage.textContent = ""
    elements.groupModal.classList.remove("hidden")
    elements.groupName.focus()
}

function closeGroupModal() {
    elements.groupModal.classList.add("hidden")
}

async function searchGroupUsers() {
    const query = elements.groupUserSearch.value.trim()
    if (query.length < 2) {
        elements.groupUserResults.innerHTML = ""
        return
    }
    try {
        const results = await api(`/api/users/search?query=${encodeURIComponent(query)}`)
        const matches = results.filter(
            (user) => !selectedGroupUsers.some((selected) => selected.id === user.id),
        )
        elements.groupUserResults.innerHTML = matches.length
            ? matches
                  .map(
                      (user) =>
                          `<button class="user-result" data-group-user-id="${user.id}" type="button"><span class="user-identity"><strong>${escapeHtml(user.username)}</strong><small>Add to this group</small></span><span>+</span></button>`,
                  )
                  .join("")
            : '<div class="empty-state">No registered username found.</div>'
        elements.groupUserResults
            .querySelectorAll("[data-group-user-id]")
            .forEach((button) =>
                button.addEventListener("click", () =>
                    addGroupUser(button.dataset.groupUserId, results),
                ),
            )
    } catch (error) {
        elements.groupMessage.textContent = error.message
    }
}

function addGroupUser(userId, results) {
    const user = results.find((candidate) => candidate.id === userId)
    if (!user || selectedGroupUsers.some((selected) => selected.id === userId)) return
    selectedGroupUsers.push(user)
    elements.groupUserSearch.value = ""
    elements.groupUserResults.innerHTML = ""
    renderGroupUsers()
}

function renderGroupUsers() {
    elements.groupSelectedUsers.innerHTML = selectedGroupUsers
        .map(
            (user) =>
                `<div class="selected-user"><span class="user-identity"><strong>${escapeHtml(user.username)}</strong><small>Group member</small></span><button class="remove-user" data-remove-group-user="${user.id}" type="button" aria-label="Remove ${escapeHtml(user.username)}">×</button></div>`,
        )
        .join("")
    elements.groupSelectedUsers.querySelectorAll("[data-remove-group-user]").forEach((button) =>
        button.addEventListener("click", () => {
            selectedGroupUsers = selectedGroupUsers.filter(
                (user) => user.id !== button.dataset.removeGroupUser,
            )
            renderGroupUsers()
        }),
    )
}

async function createGroup(event) {
    event.preventDefault()
    try {
        await api("/api/groups", {
            method: "POST",
            body: JSON.stringify({
                name: elements.groupName.value.trim(),
                memberUsernames: selectedGroupUsers.map((user) => user.username),
            }),
        })
        closeGroupModal()
        showToast("Group created.")
        await loadDashboard()
    } catch (error) {
        showToast(error.message)
    }
}

function openExpenseModal() {
    selectedExpenseParticipants = []
    elements.expenseForm.reset()
    elements.expenseCurrency.value = "GBP"
    elements.expenseUserResults.innerHTML = ""
    elements.expenseMessage.textContent = ""
    populateExpenseGroups()
    setExpenseMode("group")
    addCurrentUserToExpense()
    renderSelectedExpenseUsers()
    elements.expenseModal.classList.remove("hidden")
    elements.expenseDescription.focus()
}

function populateExpenseGroups() {
    elements.expenseGroup.innerHTML = groups.length
        ? groups
              .map((group) => `<option value="${group.id}">${escapeHtml(group.name)}</option>`)
              .join("")
        : '<option value="">No groups yet</option>'
    if (selectedGroup && groups.some((group) => group.id === selectedGroup.id))
        elements.expenseGroup.value = selectedGroup.id
    selectedGroup = groups.find((group) => group.id === elements.expenseGroup.value) || null
}

function setExpenseMode(mode) {
    expenseMode = mode
    document
        .querySelectorAll("[data-expense-mode]")
        .forEach((button) =>
            button.classList.toggle("is-active", button.dataset.expenseMode === mode),
        )
    const groupMode = mode === "group"
    elements.expenseGroupFields.classList.toggle("hidden", !groupMode)
    elements.expenseUserField.classList.toggle("hidden", groupMode)
    elements.expenseGroup.required = groupMode
    elements.expenseUserHint.textContent = groupMode
        ? "Search existing members of this group."
        : "Search any username. This sends a direct expense invitation without adding a group member."
    selectedExpenseParticipants = []
    addCurrentUserToExpense()
    renderSelectedExpenseUsers()
}

function addCurrentUserToExpense() {
    if (expenseMode !== "group" || !selectedGroup) return
    // A group expense starts with every group member; the amounts are split evenly and can be edited.
    selectedExpenseParticipants = [...selectedGroup.members]
}

function closeExpenseModal() {
    elements.expenseModal.classList.add("hidden")
}

// Search is global for direct expenses and limited to the selected group for group expenses.
async function searchExpenseUsers() {
    const query = elements.expenseUserSearch.value.trim()
    if (query.length < 2) {
        elements.expenseUserResults.innerHTML = ""
        return
    }
    try {
        const results = await api(`/api/users/search?query=${encodeURIComponent(query)}`)
        const groupMemberIds = new Set(selectedGroup?.members.map((member) => member.userId) || [])
        const matches = results.filter(
            (user) =>
                (expenseMode === "direct" || groupMemberIds.has(user.id)) &&
                !selectedExpenseParticipants.some((member) => member.userId === user.id),
        )
        elements.expenseUserResults.innerHTML = matches.length
            ? matches
                  .map((user) => {
                      return `<button class="user-result" data-user-id="${user.id}" type="button"><span class="user-identity"><strong>${escapeHtml(user.username)}</strong><small>${expenseMode === "direct" ? "Direct expense recipient" : "Group member"}</small></span><span>Select</span></button>`
                  })
                  .join("")
            : `<div class="empty-state">${expenseMode === "direct" ? "No registered username found." : "No matching member in this group."}</div>`
        elements.expenseUserResults
            .querySelectorAll("[data-user-id]")
            .forEach((button) =>
                button.addEventListener("click", () =>
                    addSearchedUser(button.dataset.userId, results),
                ),
            )
    } catch (error) {
        elements.expenseMessage.textContent = error.message
    }
}

async function addSearchedUser(userId, results) {
    const user = results.find((candidate) => candidate.id === userId)
    if (!user) return
    try {
        if (expenseMode === "direct") {
            selectedExpenseParticipants = [
                { userId: user.id, username: user.username, displayName: user.username, email: "" },
            ]
            renderSelectedExpenseUsers()
        } else addExpenseParticipant(userId)
        elements.expenseUserSearch.value = ""
        elements.expenseUserResults.innerHTML = ""
    } catch (error) {
        elements.expenseMessage.textContent = error.message
    }
}

function addExpenseParticipant(userId) {
    const member = selectedGroup.members.find((candidate) => candidate.userId === userId)
    if (member && !selectedExpenseParticipants.some((candidate) => candidate.userId === userId))
        selectedExpenseParticipants.push(member)
    elements.expenseUserSearch.value = ""
    elements.expenseUserResults.innerHTML = ""
    renderSelectedExpenseUsers()
}

function removeExpenseParticipant(userId) {
    selectedExpenseParticipants = selectedExpenseParticipants.filter(
        (member) => member.userId !== userId,
    )
    renderSelectedExpenseUsers()
}

function renderSelectedExpenseUsers() {
    elements.expenseSelectedUsers.innerHTML = selectedExpenseParticipants
        .map(
            (member) =>
                `<div class="selected-user"><span class="user-identity"><strong>${escapeHtml(member.username)}</strong><small>${escapeHtml(member.email || "group member")}</small></span><input class="split-amount" data-split-user="${member.userId}" type="number" min="0.0001" step="0.0001" aria-label="Split for ${escapeHtml(member.username)}"><button class="remove-user" data-remove-user="${member.userId}" type="button" aria-label="Remove ${escapeHtml(member.username)}">×</button></div>`,
        )
        .join("")
    elements.expenseSelectedUsers
        .querySelectorAll("[data-remove-user]")
        .forEach((button) =>
            button.addEventListener("click", () =>
                removeExpenseParticipant(button.dataset.removeUser),
            ),
        )
    resetExpenseSplitAmounts()
}

// Allocate in ten-thousandths and put the remainder on the final participant so decimal sums stay exact.
function resetExpenseSplitAmounts() {
    const fields = [...elements.expenseSelectedUsers.querySelectorAll("[data-split-user]")]
    if (!fields.length) return
    const totalUnits = Math.round((Number(elements.expenseAmount.value) || 0) * 10000)
    if (expenseMode === "direct") {
        fields[0].value = (totalUnits / 20000).toFixed(4)
        return
    }
    const baseUnits = Math.floor(totalUnits / fields.length)
    fields.forEach((field, index) => {
        const units =
            index === fields.length - 1 ? totalUnits - baseUnits * (fields.length - 1) : baseUnits
        field.value = (units / 10000).toFixed(4)
    })
}

async function createExpense(event) {
    event.preventDefault()
    const amount = Number(elements.expenseAmount.value)
    const description = elements.expenseDescription.value.trim()
    const currency = elements.expenseCurrency.value.trim().toUpperCase()
    const splits = [...elements.expenseSelectedUsers.querySelectorAll("[data-split-user]")].map(
        (field) => ({ userId: field.dataset.splitUser, amount: Number(field.value) }),
    )
    if (expenseMode === "direct" && splits.length !== 1) {
        elements.expenseMessage.textContent =
            "Search for exactly one username for a direct expense."
        return
    }
    if (expenseMode === "group" && (!selectedGroup || !splits.length)) {
        elements.expenseMessage.textContent = "Choose a group and at least one participant."
        return
    }
    if (!description) {
        elements.expenseMessage.textContent = "Enter an expense description."
        return
    }
    if (!Number.isFinite(amount) || amount <= 0) {
        elements.expenseMessage.textContent = "Enter a valid positive amount."
        return
    }
    if (!/^[A-Z]{3}$/.test(currency)) {
        elements.expenseMessage.textContent = "Use a three-letter uppercase currency code."
        return
    }
    const splitTotal = splits.reduce((sum, split) => sum + split.amount, 0)
    const invalidAmount =
        expenseMode === "direct"
            ? !amount || splitTotal <= 0 || splitTotal >= amount
            : !amount || Math.abs(splitTotal - amount) > 0.0000001
    if (invalidAmount) {
        elements.expenseMessage.textContent =
            "Add at least one person and make the split amounts equal the total."
        return
    }
    try {
        const payload = {
            description,
            amount,
            currency,
            spentAtUtc: new Date().toISOString(),
        }
        const path =
            expenseMode === "direct" ? "/api/expenses" : `/api/groups/${selectedGroup.id}/expenses`
        if (expenseMode === "direct") {
            payload.username = selectedExpenseParticipants[0].username
            payload.otherUserShare = splits[0].amount
        } else payload.splits = splits
        await api(path, {
            method: "POST",
            body: JSON.stringify(payload),
        })
        closeExpenseModal()
        showToast(expenseMode === "direct" ? "Expense invitation sent." : "Expense added to group.")
        await loadDashboard()
    } catch (error) {
        showToast(error.message)
    }
}

async function loadNotifications() {
    try {
        const notifications = await api("/api/notifications")
        elements.notificationCount.textContent = notifications.length
        renderNotifications(notifications)
    } catch (error) {
        showToast(error.message)
    }
}

function renderNotifications(notifications) {
    if (!notifications.length) {
        elements.notificationsList.innerHTML =
            '<div class="empty-state">No pending invitations.</div>'
        return
    }
    elements.notificationsList.innerHTML = notifications
        .map(
            (notification) =>
                `<div class="notification-row"><div class="notification-copy"><span class="notification-title">${escapeHtml(notification.payerName)} added you to ${escapeHtml(notification.description)}</span><span class="notification-meta">${escapeHtml(notification.groupName)} · ${Number(notification.splitAmount).toFixed(2)} ${escapeHtml(notification.currency)}</span></div><div class="notification-actions"><button class="button button-primary button-small" data-accept-notification="${notification.id}" type="button">Accept</button><button class="button button-light button-small" data-dismiss-notification="${notification.id}" type="button">Dismiss</button></div></div>`,
        )
        .join("")
    elements.notificationsList
        .querySelectorAll("[data-accept-notification]")
        .forEach((button) =>
            button.addEventListener("click", () =>
                respondToNotification(button.dataset.acceptNotification, "accept"),
            ),
        )
    elements.notificationsList
        .querySelectorAll("[data-dismiss-notification]")
        .forEach((button) =>
            button.addEventListener("click", () =>
                respondToNotification(button.dataset.dismissNotification, "dismiss"),
            ),
        )
}

async function respondToNotification(notificationId, action) {
    try {
        await api(`/api/notifications/${notificationId}${action === "accept" ? "/accept" : ""}`, {
            method: action === "accept" ? "POST" : "DELETE",
        })
        showToast(action === "accept" ? "Expense accepted." : "Invitation dismissed.")
        await loadNotifications()
    } catch (error) {
        showToast(error.message)
    }
}

async function createBudget(event) {
    event.preventDefault()
    if (!selectedGroup) {
        elements.budgetMessage.textContent = "Choose a group first."
        return
    }
    const form = new FormData(event.currentTarget)
    const payload = {
        name: form.get("budget-name") || document.querySelector("#budget-name").value,
        limit: Number(document.querySelector("#budget-limit").value),
        currency: document.querySelector("#budget-currency").value.toUpperCase(),
        startsOn: document.querySelector("#budget-start").value,
        endsOn: document.querySelector("#budget-end").value,
    }
    try {
        await api(`/api/groups/${selectedGroup.id}/budgets`, {
            method: "POST",
            body: JSON.stringify(payload),
        })
        elements.budgetMessage.textContent = "Budget saved."
        event.currentTarget.reset()
    } catch (error) {
        elements.budgetMessage.textContent = error.message
    }
}

async function convertCurrency(event) {
    event.preventDefault()
    const amount = document.querySelector("#convert-amount").value
    const from = document.querySelector("#convert-from").value.toUpperCase()
    const to = document.querySelector("#convert-to").value.toUpperCase()
    try {
        const result = await api(
            `/api/currency/convert?amount=${encodeURIComponent(amount)}&from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}`,
        )
        elements.conversionResult.textContent = `${Number(result.amount).toFixed(2)} ${result.from} = ${Number(result.convertedAmount).toFixed(2)} ${result.to} · rate ${Number(result.rate).toFixed(4)}`
    } catch (error) {
        elements.conversionResult.textContent = error.message
    }
}

function showDashboard() {
    elements.authView.classList.add("hidden")
    elements.dashboard.classList.remove("hidden")
    elements.logout.classList.remove("hidden")
    elements.notificationsButton.classList.remove("hidden")
    elements.userName.textContent = `@${session.user.username}`
    elements.headingName.textContent = session.user.username
    loadDashboard()
}
function showAuth() {
    elements.authView.classList.remove("hidden")
    elements.dashboard.classList.add("hidden")
    elements.logout.classList.add("hidden")
    elements.notificationsButton.classList.add("hidden")
    elements.userName.textContent = ""
}
function showToast(message) {
    elements.toast.textContent = message
    elements.toast.classList.add("is-visible")
    window.setTimeout(() => elements.toast.classList.remove("is-visible"), 3200)
}
function escapeHtml(value) {
    return String(value).replace(
        /[&<>'"]/g,
        (character) =>
            ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", "'": "&#39;", '"': "&quot;" })[character],
    )
}

// A stored token restores the dashboard, but all real authorization still happens on the server.
if (session.accessToken && session.user) showDashboard()
else showAuth()
