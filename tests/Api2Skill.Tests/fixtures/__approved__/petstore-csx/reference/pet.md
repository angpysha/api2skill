# pet

Everything about your Pets

## findPetsByStatus

`GET /pet/findByStatus`

Finds Pets by status

Multiple status values can be provided with comma separated strings.

**Parameters**

| name | in | required | type | enum | description |
|---|---|---|---|---|---|
| `status` | Query | yes | String | `available`, `pending`, `sold` | Status values that need to be considered for filter |

- `status` example: `available`

**Request body**: none

**Auth**: `petstore_auth`

**Responses**

### `200`: successful operation

- Content-Type: `application/json`
- Shape: array<object { id, name, status, category, tags }>

| property | type | required | enum | description |
|---|---|---|---|---|
| `items` | object { id, name, status, category, tags } | no |  |  |
| `items.id` | Integer (int64) | yes |  | Pet id |
| `items.name` | String | yes |  | Pet name |
| `items.status` | String enum { available, pending, sold } | no | `available`, `pending`, `sold` | Pet status in the store |
| `items.category` | object { id, name } | no |  |  |
| `items.category.id` | Integer (int64) | no |  | Category id |
| `items.category.name` | String | no |  | Category name |
| `items.tags` | array<object { id, name }> | no |  | Tags attached to this pet |
| `items.tags[]` | object { id, name } | no |  |  |
| `items.tags[].id` | Integer (int64) | no |  | Tag id |
| `items.tags[].name` | String | no |  | Tag name |

Example:

```json
[
  {
    "id": 0,
    "name": "string",
    "status": "available",
    "category": {
      "id": 0,
      "name": "string"
    },
    "tags": [
      {
        "id": 0,
        "name": "string"
      }
    ]
  }
]
```

_Nested fields truncated at depth 4._

### `400`: Invalid status value

- Body: none documented in the OpenAPI response

---

## getPetById

`GET /pet/{petId}`

Find pet by ID

Returns a single pet.

**Parameters**

| name | in | required | type | enum | description |
|---|---|---|---|---|---|
| `petId` | Path | yes | Integer (int64) |  | ID of pet to return |

**Request body**: none

**Auth**: `api_key`

**Responses**

### `200`: successful operation

- Content-Type: `application/json`
- Schema: [`Pet`](schemas/Pet.json)
- Shape: object { id, name, status, category, tags }

| property | type | required | enum | description |
|---|---|---|---|---|
| `id` | Integer (int64) | yes |  | Pet id |
| `name` | String | yes |  | Pet name |
| `status` | String enum { available, pending, sold } | no | `available`, `pending`, `sold` | Pet status in the store |
| `category` | object { id, name } | no |  |  |
| `category.id` | Integer (int64) | no |  | Category id |
| `category.name` | String | no |  | Category name |
| `tags` | array<object { id, name }> | no |  | Tags attached to this pet |
| `tags[]` | object { id, name } | no |  |  |
| `tags[].id` | Integer (int64) | no |  | Tag id |
| `tags[].name` | String | no |  | Tag name |

Example:

```json
{
  "id": 0,
  "name": "string",
  "status": "available",
  "category": {
    "id": 0,
    "name": "string"
  },
  "tags": [
    {
      "id": 0,
      "name": "string"
    }
  ]
}
```

### `404`: Pet not found

- Body: none documented in the OpenAPI response

---

## addPet

`POST /pet`

Add a new pet to the store

**Request body**

- Content-Type: `application/json` (required)
- Schema: [`PetInput`](schemas/PetInput.json)
- Shape: object { name, status, category, tags }

| property | type | required | enum | description |
|---|---|---|---|---|
| `name` | String | yes |  | Pet name |
| `status` | String enum { available, pending, sold } | no | `available`, `pending`, `sold` | Pet status in the store |
| `category` | object { id, name } | no |  |  |
| `category.id` | Integer (int64) | no |  | Category id |
| `category.name` | String | no |  | Category name |
| `tags` | array<object { id, name }> | no |  | Tags attached to this pet |
| `tags[]` | object { id, name } | no |  |  |
| `tags[].id` | Integer (int64) | no |  | Tag id |
| `tags[].name` | String | no |  | Tag name |

Example:

```json
{
  "name": "string",
  "status": "available",
  "category": {
    "id": 0,
    "name": "string"
  },
  "tags": [
    {
      "id": 0,
      "name": "string"
    }
  ]
}
```

**Auth**: `petstore_auth`

**Responses**

### `200`: successful operation

- Content-Type: `application/json`
- Schema: [`Pet`](schemas/Pet.json)
- Shape: object { id, name, status, category, tags }

| property | type | required | enum | description |
|---|---|---|---|---|
| `id` | Integer (int64) | yes |  | Pet id |
| `name` | String | yes |  | Pet name |
| `status` | String enum { available, pending, sold } | no | `available`, `pending`, `sold` | Pet status in the store |
| `category` | object { id, name } | no |  |  |
| `category.id` | Integer (int64) | no |  | Category id |
| `category.name` | String | no |  | Category name |
| `tags` | array<object { id, name }> | no |  | Tags attached to this pet |
| `tags[]` | object { id, name } | no |  |  |
| `tags[].id` | Integer (int64) | no |  | Tag id |
| `tags[].name` | String | no |  | Tag name |

Example:

```json
{
  "id": 0,
  "name": "string",
  "status": "available",
  "category": {
    "id": 0,
    "name": "string"
  },
  "tags": [
    {
      "id": 0,
      "name": "string"
    }
  ]
}
```

---

