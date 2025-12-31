# Square Credit Card Processing - Setup Guide

## 🎯 Overview

This guide will help you set up Square payment processing for your dispatch application. Square handles all credit card tokenization securely, so your servers never touch raw card data (PCI compliant!).

---

## 📋 Prerequisites

- A Square account (sandbox for testing, production for live payments)
- Basic knowledge of where to edit config files

---

## 🚀 Step 1: Create a Square Developer Account

### 1.1 Sign Up for Square

1. Go to: https://developer.squareup.com
2. Click **"Get Started"**
3. Sign up with your email or Google account
4. Verify your email

### 1.2 Create a Sandbox Application

1. Once logged in, go to **Applications** in the left menu
2. Click **"Create Application"** (or it might already exist)
3. Name it something like "Dispatch App - Sandbox"
4. This creates a **Sandbox** application for testing

### 1.3 Get Your Credentials

In your application dashboard, you'll see:

**📌 Application ID** (Sandbox)

- Looks like: `sandbox-sq0idb-XXXXXXXXXXXX`
- This is PUBLIC and goes in your frontend code

**📌 Access Token** (Sandbox)

- Looks like: `EAAAl...` (very long string)
- This is SECRET and goes in your server config
- ⚠️ **NEVER** put this in frontend code!

**📌 Location ID**

- Every Square account has at least one location
- Go to **Locations** tab
- Copy the Location ID (starts with `L...`)

---

## ⚙️ Step 2: Configure Your Backend (Server)

### File: `DispatchApp.Server/appsettings.json`

Find the `SquareSettings` section and replace placeholders:

```json
"SquareSettings": {
  "ApplicationId": "sandbox-sq0idb-YOUR_APP_ID_HERE",
  "AccessToken": "YOUR_SANDBOX_ACCESS_TOKEN_HERE",
  "Environment": "Sandbox",
  "LocationId": "YOUR_LOCATION_ID_HERE"
}
```

**Example:**

```json
"SquareSettings": {
  "ApplicationId": "sandbox-sq0idb-abc123xyz789",
  "AccessToken": "EAAAl1234567890abcdefghijklmnopqrstuvwxyz",
  "Environment": "Sandbox",
  "LocationId": "L1234ABCD5678"
}
```

---

## 🌐 Step 3: Configure Your Frontend (DispatchApp)

### File: `DispatchApp.client/src/components/SquarePaymentForm.jsx`

Find these lines (around line 38-40):

```javascript
const SQUARE_APPLICATION_ID = 'YOUR_SQUARE_APPLICATION_ID_HERE';
const SQUARE_LOCATION_ID = 'YOUR_SQUARE_LOCATION_ID_HERE';
```

Replace with YOUR values from Step 1.3:

```javascript
const SQUARE_APPLICATION_ID = 'sandbox-sq0idb-abc123xyz789';
const SQUARE_LOCATION_ID = 'L1234ABCD5678';
```

⚠️ **Use the SAME Application ID and Location ID from appsettings.json!**

---

## 📱 Step 4: Configure Driver App (React Native)

### File: `DriverApp/src/config/environment.js` (or create if doesn't exist)

Add Square configuration:

```javascript
export const SQUARE_APPLICATION_ID = 'sandbox-sq0idb-abc123xyz789'; // Same as above
export const SQUARE_LOCATION_ID = 'L1234ABCD5678';
```

---

## 🧪 Step 5: Run Database Migration

The system needs a new field in the Ride table to store payment tokens.

Open a terminal in the server directory:

```bash
cd DispatchApp.Server
dotnet ef database update
```

This adds the `PaymentTokenId` field to your Ride table.

---

## ✅ Step 6: Test Your Setup

### 6.1 Start Your Applications

**Terminal 1 - Backend:**

```bash
cd DispatchApp.Server
dotnet run
```

**Terminal 2 - DispatchApp:**

```bash
cd DispatchApp.client
npm run dev
```

**Terminal 3 - DriverApp:**

```bash
cd DriverApp
npx expo start
```

### 6.2 Test Card Tokenization (Dispatcher App)

1. Log in to DispatchApp
2. Go to create a new call (New Call Wizard)
3. Select **"Dispatcher CC"** as payment type
4. Credit card form should appear below
5. Enter test card (see below)
6. Form should tokenize card automatically when you blur/tab away
7. Submit the call

### 6.3 Test Charging (Driver App)

1. Log in to DriverApp
2. Pick up the ride you just created
3. Complete the ride (click "Dropped Off")
4. On the payment screen, click **"Charge"**
5. Should see success message

---

## 💳 Test Credit Cards (Sandbox Mode)

Use these FAKE cards in Sandbox mode (they won't charge real money):

### ✅ Successful Payment

- **Card Number:** `4111 1111 1111 1111` (Visa)
- **Expiration:** Any future date (e.g., `12/25`)
- **CVV:** Any 3 digits (e.g., `123`)
- **ZIP:** Any 5 digits (e.g., `12345`)

### ❌ Declined Card

- **Card Number:** `4000 0000 0000 0002`
- **Expiration:** Any future date
- **CVV:** Any 3 digits
- Result: Card will be declined

### ❌ Insufficient Funds

- **Card Number:** `4000 0000 0000 9995`
- **Expiration:** Any future date
- **CVV:** Any 3 digits
- Result: Insufficient funds error

More test cards: https://developer.squareup.com/docs/devtools/sandbox/payments

---

## 🚀 Step 7: Go Live (Production)

When ready for real payments:

### 7.1 Get Production Credentials

1. Go to Square Dashboard: https://squareup.com/dashboard
2. Go to your application
3. Switch to **Production** tab
4. Get Production Access Token

### 7.2 Update Backend Config

In `appsettings.json`:

```json
"SquareSettings": {
  "ApplicationId": "sq0idp-YOUR_PRODUCTION_APP_ID",
  "AccessToken": "YOUR_PRODUCTION_ACCESS_TOKEN",
  "Environment": "Production",  // ← Change this!
  "LocationId": "YOUR_PRODUCTION_LOCATION_ID"
}
```

### 7.3 Update Frontend (index.html)

Change Square SDK URL from sandbox to production:

**Before (Sandbox):**

```html
<script src="https://sandbox.web.squarecdn.com/v1/square.js"></script>
```

**After (Production):**

```html
<script src="https://web.squarecdn.com/v1/square.js"></script>
```

### 7.4 Update Frontend Code (SquarePaymentForm.jsx)

Replace Application ID with production one:

```javascript
const SQUARE_APPLICATION_ID = 'sq0idp-YOUR_PRODUCTION_APP_ID'; // Note: sq0idp not sandbox-sq0idb
```

---

## 🔐 Security Best Practices

1. ✅ **NEVER** commit Access Tokens to git
2. ✅ **NEVER** put Access Tokens in frontend code
3. ✅ Use environment variables for production
4. ✅ Keep `appsettings.json` in `.gitignore`
5. ✅ Rotate tokens periodically

---

## 🐛 Troubleshooting

### "Square SDK failed to load"

- Check that `<script>` tag is in `index.html`
- Check browser console for errors
- Try hard refresh (Ctrl+Shift+R)

### "Failed to initialize Square Payments"

- Verify Application ID is correct
- Verify Location ID is correct
- Check browser console for specific error

### "Payment failed: AUTHENTICATION_ERROR"

- Backend Access Token is wrong
- Or Environment setting doesn't match (Sandbox vs Production)

### "Payment failed: CARD_DECLINED"

- If in Sandbox: Use test card `4111 1111 1111 1111`
- If in Production: Real card issue, try different card

### Server says "Square Access Token is not configured"

- You didn't update `appsettings.json`
- Server needs restart after changing config

---

## 📚 Additional Resources

- **Square Developer Docs:** https://developer.squareup.com/docs
- **Web Payments SDK Guide:** https://developer.squareup.com/docs/web-payments/overview
- **Test Values:** https://developer.squareup.com/docs/devtools/sandbox/payments
- **Square Support:** https://squareup.com/help/us/en

---

## 🎉 You're Done!

Your dispatch app now processes credit cards securely through Square!

**What happens:**

1. ✅ Dispatcher enters card → Square tokenizes it (secure)
2. ✅ Token saved with ride (safe to store)
3. ✅ Driver completes ride → Server charges token
4. ✅ Money goes to your Square account

**You're PCI compliant because:**

- ✅ Raw card data never touches your servers
- ✅ Only secure tokens are stored
- ✅ Square handles all the sensitive stuff
