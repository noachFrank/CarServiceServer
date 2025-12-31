# JWT Authentication Implementation Guide

## Overview

This document explains the JWT (JSON Web Token) authentication system implemented for the Dispatch Application.

## What Was Implemented

### 1. Server-Side (ASP.NET Core)

#### A. JWT Service (`Services/JwtService.cs`)

- **Purpose**: Generates and validates JWT tokens
- **Key Methods**:
  - `GenerateToken()`: Creates a JWT with user claims (userId, name, userType, isAdmin)
  - `ValidateToken()`: Validates incoming JWT tokens

#### B. Authentication Configuration (`Program.cs`)

- **JWT Bearer Authentication** added to the middleware pipeline
- **Token Validation Parameters**:
  - Validates signature using secret key
  - Validates issuer and audience
  - Validates token lifetime (expiration)
  - Zero clock skew (strict expiration)

#### C. SignalR JWT Support

- **Special Configuration**: SignalR reads JWT from query string (`access_token` parameter)
- **Why**: WebSocket connections can't use Authorization headers, so token is passed as query parameter
- **Implementation**: Custom `OnMessageReceived` event handler in Program.cs

#### D. Controller Protection

- **[Authorize] Attribute**: Added to all controller classes

  - `RideController`: All ride endpoints require authentication
  - `UserController`: All endpoints except `/login` require authentication
  - `CommunicationController`: All message endpoints require authentication
  - `Dispatch Hub`: SignalR hub requires authentication

- **[AllowAnonymous] Attribute**: Only on the `/api/user/login` endpoint

### 2. Configuration (`appsettings.json`)

```json
"JwtSettings": {
  "SecretKey": "YourSuperSecretKeyThatIsAtLeast32CharactersLong!ChangeThisInProduction!",
  "Issuer": "DispatchApp",
  "Audience": "DispatchAppUsers",
  "ExpirationMinutes": "60"
}
```

⚠️ **IMPORTANT**: Change the `SecretKey` before deploying to production! Use environment variables in production.

### 3. Client-Side (React)

#### A. Token Management (`config/apiConfig.js`)

Already implemented:

- `tokenManager`: Stores/retrieves tokens from localStorage
- **Request Interceptor**: Automatically adds `Authorization: Bearer {token}` to all API requests
- **Response Interceptor**: Handles 401 errors and token refresh

#### B. SignalR Token Support (`services/signalRService.js`)

Updated to include JWT token:

```javascript
.withUrl(hubUrl, {
  skipNegotiation: true,
  transport: signalR.HttpTransportType.WebSockets,
  accessTokenFactory: () => token // JWT token for authentication
})
```

#### C. Authentication Context (`contexts/AuthContext.jsx`)

Updated to handle new login response format:

- Stores token from `data.token`
- Extracts user details from `data.userDetails`
- Maintains backward compatibility

## How It Works

### Login Flow:

1. **User submits credentials** → Frontend sends POST to `/api/user/login`
2. **Server validates credentials** → Checks password hash
3. **Server generates JWT** → Creates token with user claims
4. **Server returns response**:
   ```json
   {
     "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
     "userId": 123,
     "name": "John Doe",
     "userType": "dispatcher",
     "isAdmin": true,
     "userDetails": {
       /* full user object */
     }
   }
   ```
5. **Client saves token** → `localStorage.setItem('dispatch_jwt_token', token)`
6. **Client includes token in requests** → Automatic via interceptor

### Authenticated Request Flow:

1. **Client makes API request** → Interceptor adds `Authorization: Bearer {token}` header
2. **Server validates token** → JWT middleware checks signature, expiration, claims
3. **If valid** → Request proceeds to controller
4. **If invalid/expired** → Server returns 401 Unauthorized
5. **Client handles 401** → Attempts token refresh or redirects to login

### SignalR Authentication Flow:

1. **Client connects to SignalR** → Includes token via `accessTokenFactory`
2. **SignalR reads token** → From query string (`access_token` parameter)
3. **Server validates token** → Same JWT validation as HTTP requests
4. **If valid** → Connection established
5. **If invalid** → Connection rejected with 401

## Security Features

### ✅ What's Protected:

1. **All POST endpoints** - Create, Update, Delete operations
2. **All GET endpoints** - Read operations (sensitive data)
3. **SignalR Hub** - Real-time WebSocket connections
4. **Token expiration** - 60-minute lifetime (configurable)
5. **Signature validation** - Prevents token tampering

### ✅ Best Practices Implemented:

1. **Secrets in configuration** - Not hardcoded
2. **HTTPS recommended** - For production (HTTP allowed for dev)
3. **Strong secret key** - Minimum 32 characters
4. **Zero clock skew** - Strict expiration enforcement
5. **Claims-based auth** - User identity in token

## Q&A: GET Endpoints Authentication

### Should GET endpoints be authenticated?

**YES! Here's why:**

#### Industry Standard Practice:

- **Public APIs**: Only public content endpoints (e.g., blog posts, product catalogs) are unauthenticated
- **Business Applications**: ALL endpoints accessing user/business data should be authenticated
- **Security Principle**: "Deny by default, allow by exception"

#### Your Application:

All your GET endpoints return sensitive data:

- `/api/Ride/AssignedRides` - Business ride information
- `/api/User/DriverById` - Personal driver information
- `/api/Communication/TodaysCom` - Private messages
- `/api/Ride/Dashboard` - Business metrics

**Leaving these unauthenticated would allow:**

- Anyone to see your rides
- Competitors to monitor your operations
- Unauthorized access to driver personal info
- Privacy violations (messages, phone numbers)

#### When GET endpoints DON'T need auth:

- Health check endpoints (`/health`, `/ping`)
- Public marketing content
- Open data APIs (weather, public records)
- Status pages

#### Conclusion:

✅ **Keep [Authorize] on all controllers**
✅ **All your endpoints need authentication**
✅ **This is the correct and secure approach**

## Testing the Implementation

### 1. Test Login

```bash
# Should work
curl -X POST http://localhost:5062/api/user/login \
  -H "Content-Type: application/json" \
  -d '{
    "userType": "dispatcher",
    "nameOrEmail": "admin",
    "password": "password"
  }'

# Response should include token:
# {"token": "eyJhbGc...", "userId": 1, "name": "Admin", ...}
```

### 2. Test Protected Endpoint WITHOUT Token

```bash
# Should return 401 Unauthorized
curl -X GET http://localhost:5062/api/Ride/Dashboard
```

### 3. Test Protected Endpoint WITH Token

```bash
# Should return 200 OK with data
curl -X GET http://localhost:5062/api/Ride/Dashboard \
  -H "Authorization: Bearer eyJhbGc..."
```

### 4. Test SignalR Connection

Open browser console and check:

- ✅ "SignalR Connected" message
- ❌ If you see 401 errors, check token is being passed

## Troubleshooting

### Issue: 401 on all requests

**Fix**: Check token is being saved and sent

```javascript
console.log('Token:', tokenManager.getToken());
```

### Issue: Token expired immediately

**Fix**: Check system clocks are synchronized (server and client)

### Issue: SignalR won't connect

**Fix**: Check token is in `accessTokenFactory`

```javascript
// In signalRService.js
accessTokenFactory: () => {
  const token = tokenManager.getToken();
  console.log('SignalR token:', token);
  return token;
};
```

### Issue: CORS errors

**Fix**: Ensure `app.UseAuthentication()` comes AFTER `app.UseCors()`

## Production Checklist

Before deploying to production:

- [ ] Change JWT secret key to a strong, unique value
- [ ] Move JWT secret to environment variable (not appsettings.json)
- [ ] Enable HTTPS only
- [ ] Set appropriate token expiration (shorter = more secure)
- [ ] Implement refresh token mechanism
- [ ] Add rate limiting on login endpoint
- [ ] Monitor for suspicious authentication patterns
- [ ] Set up token revocation if user logout
- [ ] Use secure cookie storage instead of localStorage (optional, more secure)

## Summary

You now have a complete JWT authentication system:
✅ Tokens generated on login
✅ Tokens required for all API endpoints (except login)
✅ Tokens validated on every request
✅ SignalR connections authenticated
✅ GET endpoints properly protected
✅ Client automatically includes tokens
✅ Proper error handling for expired/invalid tokens

This is **industry-standard** authentication for your application type.
